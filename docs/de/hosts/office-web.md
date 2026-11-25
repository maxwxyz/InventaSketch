# PowerPoint-Web-Add-in (Office.js)

> English (maßgeblich): [../../en/hosts/office-web.md](../../en/hosts/office-web.md)

## Architektur

- **Engine:** die C#-Engine (`src/InventaSketch.Engine` + Module), kompiliert zu .NET WebAssembly durch
  `hosts/office-web/engine/InventaSketch.Web.Engine.csproj` (`Microsoft.NET.Sdk.WebAssembly`, kein Workload nötig).
  TypeScript ruft sie über `[JSExport]`-Methoden mit JSON-Strings auf (`src/engine/*`). Größe: ca. 2,1 MB Brotli;
  Start im Browser ca. 0,5 s (warm) bis 5 s (kalt, gemessen am 2026-11-20 im Dev-Build).
- **Oberfläche (TypeScript, Vite):** Bibliothek mit Suche (EN/DE), Kategorien und Favoriten; Bühne mit
  Auswahlgriffen, Fanghilfslinien, Einfügen am Mauszeiger, Kreismenü (Rechtsklick / `Umschalt+F10`), schwebende
  Kontextleiste, Inspektor; Hell/Dunkel/Automatisch; automatische Sprache Deutsch/Englisch (`src/ui/*`,
  `src/theme.ts`, `src/i18n.ts`).
- **Office-Adapter:** `src/office/*` übersetzt Zeichenpläne der Engine in PowerPoint-Formen (abhängig von den
  Host-Fähigkeiten), reiht alle `PowerPoint.run`-Aufrufe in eine Warteschlange und speichert IDs/Modell in Shape-Tags.
- **Manifest:** erzeugt von `scripts/manifest.mjs` (Basis-URL als Argument oder `ISK_BASE_URL`), mit
  `EquivalentAddins` auf das COM-Add-in `InventaSketch.AddIn.Connect`, sodass Windows das Web-Add-in ausblendet,
  solange das COM-Add-in installiert ist.

## Engine-API

Alle Aufrufe laufen über `EngineExports.Call(method, json)` und liefern `{"ok":true,"result":…}` oder
`{"ok":false,"error":"…"}` (`src/InventaSketch.Engine/EngineApi.cs`, Anfrageobjekt `Contracts.cs`, typisierter Client
`src/engine/client.ts`). Anfragen sind camelCase-JSON; Geometrie in mm (Zeichenpläne und Host-Rechtecke in pt). Jede
Bearbeitungsmethode erhält `session`; `ids` ist standardmäßig die aktuelle Auswahl. Änderungen liefern den
Editorzustand und sind je ein Rückgängig-Schritt (kein Schritt, wenn sich nichts ändert).

| Methode | Anfrage | Ergebnis |
|---|---|---|
| `version` | – | Engine-Version, Module, Methodenliste |
| `createSession`, `newDocument`, `openDocument` | `lang`, `pageWidth`, `pageHeight`, `title`, `json` | Zustand |
| `saveDocument` | `indented` | `{ json }` |
| `setPage` | `pageWidth`, `pageHeight`, `lang` | Zustand |
| `state` | `includeSvg` (Standard true) | Zustand |
| `catalog` | `lang` | Kategorien und Bibliothekselemente mit Vorschaubildern |
| `insert` | `itemId`, `x`, `y`, `snap`, `tolerance` | Zustand (neues Objekt ausgewählt) |
| `move`, `scale`, `resize`, `rotate`, `flip` | `dx`/`dy`; `pivotX`/`pivotY`, `sx`/`sy`; `id`, `w`, `h`, `x`, `y`; `degrees`; `horizontal` | Zustand |
| `duplicate`, `delete`, `group`, `ungroup`, `reorder` | `ids`, `dx`/`dy`, `to` (front, back, forward, backward) | Zustand |
| `setView` | `view` | Zustand; Größe verzerrungsfrei angepasst (`Registry.WithView`) |
| `setParam`, `setStyle`, `setText`, `setPoint` | `key`/`value`; `style`/`replace`; `text`; `id`/`index`/`x`/`y`/`snap`/`anchor` (false: einrasten ohne anzuhängen) | Zustand |
| `setMainText` | `id` (Standard: erstes ausgewähltes Objekt mit Haupttext), `text` | Zustand; setzt den Haupttext des Objekts (unten) |
| `setPlanScale` | `value` (> 0 setzt, null löscht den Planmaßstab des Dokuments, 100 = 1:100) | Zustand |
| `setMonochrome` | `value` (true/false) | Zustand; Schwarz-Weiß für das Dokument, jedes Objekt gilt als geändert |
| `importData` | `text` (CSV/TSV), `id` (optional Diagramm) | Zustand; die Tabelle als Daten des ausgewählten oder eines neuen Diagramms |
| `importGeo` | `text` (GeoJSON oder GPX) | Zustand; Routen und Marker in die Seite eingepasst, ausgewählt |
| `importVector` | `text` (SVG oder ASCII-DXF) | Zustand; bearbeitbare Grundformen in einer Gruppe (echte Größe, wenn die Datei Einheiten hat und passt, sonst eingepasst), ausgewählt; ein Rückgängig-Schritt |
| `preflight` | `text` (Profil-ID, Standard "common-safe"), `lang` | `{ profile, disclaimer, findings: [{ rule, severity, id, page, message, suggestion, source }] }` – Kompatibilitätsprüfung der Zeichnungen |
| `patentProfiles` | `lang` | die Ämter `{ id, name }` |
| `sanitize` | `ids` (keine = jedes Element der Seite), `mode` ("normalised", "randomised", "none"), `value` `{ renameCategories, replaceTexts, stripMeta, seed }`, `lang` | `{ state, report: { mode, seed, elements, changed, values, texts, metadata, references, unknown, failed, byType, summary, lines } }` – „Folien bereinigen“; ein Rückgängig-Schritt |
| `diff` | `payload` (kompaktes Modell des früheren Stands, z. B. der vorherigen Folie) | `{ added, changed, removed: [{ id, x, y, w, h }] }` – Varianten: was sich von diesem Stand unterscheidet (Rahmen in mm, entfernte aus dem früheren Stand) |
| `setAccent` | `text` (Voreinstellung aus `accents`, #rrggbb oder "" für die Originalfarben) | Zustand (`accent`); Akzentfarbe des Dokuments, jedes Objekt gilt als geändert |
| `accents` | `lang` | die angebotenen Akzentfarben `{ id, color, name }` |
| `setLineGroup` | `value` (0,25, 0,35, 0,5, 0,7 oder 1 – Breite der breiten Linie in mm) | Zustand (`lineGroup`); ISO-128-Liniengruppe des Dokuments, jedes Objekt gilt als geändert |
| `applyAction` | `action` (ID aus `contextActions`), `input` bei Eingabeaktionen | Zustand |
| `undo`, `redo`, `select`, `selectRect` | `ids`; `x`, `y`, `w`, `h`, `mode` (enclosed/touching), `add` | Zustand |
| `hitTest` | `x`, `y`, `tolerance` | `{ id, ids, stack }` |
| `snapMove`, `snapPoint`, `snapInsert`, `setSnap` | `dx`/`dy`; `x`/`y`/`exclude`; `itemId`; `snapOptions` | Fangergebnis `{ dx, dy, x, y, kind, guides, attach, label }`; `label` (snapMove) nennt, woran das Ziehen andockt („Teilkreis“, „Achse“, „Bolzen 2“) |
| `contextActions`, `inspector` | `ids`, `lang` | Aktionen (allgemein, Ansichten, Typaktionen); Inspektordaten |
| `suggestions` | `ids`, `lang`, `max` (Standard 9) | Bibliothekselemente `{ id, typeId, name, view, category, points }`, die zum ersten ausgewählten Objekt passen („Einfügen ▸“: Begleiter, gleiche Kategorie, Beschriftungen, in seiner Art von Ansicht); leer ohne Auswahl |
| `conversions`, `convert` | `ids`, `lang`; `ids`, `itemId` (Zieltyp) | Linientypen, in die die erste ausgewählte Linie, der Pfeil, das Maß oder die Hinweislinie umgewandelt werden kann („Typ ▸“, `{ id, label, active }`); Zustand (Punkte, Enden und Anhänge bleiben) |
| `quickAdd`, `nextItems`, `replaceItem` | `id`, `side` (left/top/right/bottom), optional `x`/`y`, `itemId`; `id`, `max`; `id`, `itemId` | Zustand mit dem neuen Element ausgewählt (Kopie plus Verbinder, ein Rückgängig-Schritt); Bibliothekselemente für das nächste Element; Zustand (Element ersetzt, Verbinder bleiben) |
| `search` | `ids`, `text`, `lang`, `recent` (Element-IDs, zuletzt verwendetes zuerst), `max` (Standard 30) | Bibliothekstreffer (Form wie `suggestions` plus `fits`): ohne Text die zum ersten ausgewählten Objekt passenden Teile, dann die zuletzt verwendeten; mit Text die ganze Bibliothek (`LibrarySearch`: beide Sprachen, Umlaute = ae/oe/ue/ss, passende und zuletzt verwendete Elemente sowie die Ansicht der Auswahl höher gewichtet) |
| `transformEffect` | `ids` | `{ mirrorH, mirrorV, rotate90 }`: ob die Transformation die gezeichnete Geometrie ändert (`Symmetry`, Pfeilspitzen eingeschlossen) |
| `arrangeOptions`, `arrange` | `ids`, `lang`; `ids`, `action` | auswahlweite Operationen, die etwas ändern `{ id, label, icon }` (`mview:<view>` eine Ansicht für alle mit erhaltenem Mechanismus, `align:row`/`align:column`, `dist:h`/`dist:v`, `match`, `renumber`); Zustand (ein Rückgängig-Schritt) |
| `assembly` | `ids` | IDs der Auswahl und aller damit gekoppelten Objekte (Getriebezug, Antrieb), Auswahl zuerst |
| `connected`, `connectOptions` | `ids`, `mode` (auto, net, coupled, chain, loop, walls); `ids`, `lang` | „Verbundene auswählen“: die IDs, die gegebenen zuerst; die Arten, die etwas hinzufügen `{ kind, label, hint, ids }` |
| `parts`, `partAt`, `partActions`, `applyPartAction`, `partOutline` | `id`, `lang`; `id`, `x`, `y`, `tolerance`; `id`, `part`; `id`, `part`, `action`, `input`; `id`, `part` | Teile eines Diagramms `{ key, label, kind, hidden }`; `{ part }`; die Aktionen des Teils (mit den Auswahlfeldern); Zustand; Umriss als Polylinien in Seiten-mm |
| `dragHandles`, `moveHandle` | `ids`, `lang`; `id`, `key`, `x`, `y`, `preview` | Griffe an Werten `[{ id, handles: [{ key, x, y, constraint, dx, dy, label, part, showWith, byPart }] }]`; Zustand (ein Rückgängig-Schritt) oder `{ handle }`, wie er wäre |
| `values`, `setValue`, `clearValue`, `explain` | `id`, `lang`, `digits`; `id`, `key`, `text`; `id`, `key`; `id`, `key`, `digits` | Wertetabelle (engineering-calculations.md 10.6); Zustand; Zustand; `{ steps, text }` |
| `calc`, `convertUnit` | `text`, `lang`, `digits`; `text`, `unit` | Rechner mit Einheiten `{ value, unit, text, steps }`; `{ value, unit, text, kind, units }` |
| `importCode` | `text`, `mode` (auto, matplotlib, pgfplots, tikz), `lang` | `{ state, format, ids, imported, skipped, message }` (gelesen, nie ausgeführt; ein Rückgängig-Schritt) |
| `setValueLevel` | `text` („quick“, „explore“, „engineer“) | Zustand (`valueLevel`); ein Rückgängig-Schritt, wenn sie sich ändert (2026-11-25 für das Web-Add-in ergänzt) |
| `trace` | `id`, `index` (Standard: letzter Punkt), `max` (Schritte, Standard 72) | `{ points, closed }`: Bahn des Gliedpunkts, während die Kurbel der Kette dreht; leer, außer die Kette hat F = 1 |
| `drawGhosts` | `id` (ein Glied), `max` (Standard 4) | Zustand; gestrichelte graue Kopien der Kette über den Kurbelbereich, gruppiert |
| `couplingPreview` | `ids`, `dx`/`dy` oder `sx`/`sy`/`pivotX`/`pivotY` | `{ ids, svg }`: Nachbarn, die die Kopplung verschieben oder skalieren würde, als SVG in Seiten-mm (`svg` leer, wenn nichts folgt); nichts wird geändert |
| `buildPorts` | `id` | `[{ id, kind, x, y, dx, dy, stretch, medium }]`: freie Bauanschlüsse in Seiten-mm mit der Richtung nach außen; `stretch` bei Wellenenden; `medium` bei Anschlüssen (`Connect`) und freien Leitungsenden (`LineEnd`) |
| `buildItems`, `buildPlace`, `buildStretch` | `id`, `key` (Anschluss-ID), `lang`, `max`; `itemId`; `x`, `y` | Bibliothekselemente, die am Anschluss andocken (`fits` true); Zustand mit dem angedockten Teil ausgewählt; Zustand mit verschobenem Wellenende |
| `motionInfo` | `id` | `{ kind: "gear", x, y, coupled }` (dreht um seine Mitte mit seinem Getriebezug), `{ kind: "linkage", dof }` (Glied einer selbst gebauten Kette mit F >= 1) oder `{ kind: "none" }` |
| `motionPreview` | `id` und `degrees` (Zahnrad) oder `id`, `index`, `x`, `y` (Gelenk eines Glieds) | `{ ids, svg }`: die Objekte, die das Drehen oder Ziehen bewegen würde, als SVG in Seiten-mm; nichts wird geändert |
| `motionTurn`, `motionDrag` | `id`, `degrees`; `id`, `index`, `x`, `y` | Zustand (je ein Rückgängig-Schritt): Getriebezug gedreht (im Uhrzeigersinn positiv); Kette mit gleichbleibenden Gliedlängen bewegt |
| `update` | `object` (vollständiges Dokumentobjekt wie in `saveDocument`) | Zustand |
| `exporters`, `export` | `ids` (Auswahl mit ihren Gruppen, sonst die Seite), `lang`; `itemId` (Exporter-Id) | zutreffende Fachexporte `{ id, name, extension, mime, preserves }`; `{ id, name, extension, mime, text }` |
| `renderPageSvg` | `ids`, `monochrome` (Standard: Dokumenteinstellung), `background`, `padding` | `{ svg, width, height }` |
| `drawPlan` | `ids`, `capabilities` | Zeichenplan in pt (schwarz-weiß, wenn das Dokument es ist) |
| `exportCompact`, `importCompact`, `hostTransform` | `ids`; `payload`, `add`; `from`, `target`, `rotation` | Payload; Zustand |

Editorzustand: `session`, `revision`, `pageId`, `pageWidth`, `pageHeight`, `grid`, `selection`, `selectionBounds`,
`canUndo`, `canRedo`, `undoLabel`, `objects`, `svg` (Seiten-SVG, schwarz-weiß, wenn das Dokument es ist), `planScale`,
`monochrome`, `valueLevel` („quick“, „explore“, „engineer“), `changed` (durch die Änderung dieser Revision hinzugefügte oder geänderte Objekte, auch bei
Rückgängig/Wiederholen; nach `setMonochrome` alle Objekte, nach `setPlanScale` alle Objekte mit Planmaßstab) und
`dependents` (weitere Objekte, deren Darstellung von den geänderten oder entfernten abhängt: Wandanschlüsse,
Öffnungen). Hosts zeichnen die Einheiten aus `changed` und `dependents` in einem Schritt neu. Jedes Objekt enthält ID,
Typ, Name, Art (box/points), Grenzen, Mitte, Größe, Drehung, Spiegelung, Punkte, Gruppe, Ansicht, Text und Bezug;
**ausgewählte** Objekte zusätzlich `centred` (Teilkreis- oder Nabenanker: standardmäßig um die Mitte skalieren), `hasValues` (der Typ hat Werte: „Werte“ in der Leiste) und
`mainText` `{ key, label, value }`: das Bezugszeichen bei Bezugszeichen und Bezugslinien, sonst der Objekttext bei
Texttypen, sonst der erste Textparameter aus text, label, name und title (`key` fehlt beim Objekttext). `importCompact`
übernimmt die Schwarz-Weiß-Einstellung des Folienmodells, damit Live-Neuzeichnungen das Aussehen der Einheiten auf der
Folie behalten.

## Bearbeitung (gleichwertig zum COM-Add-in, 2026-11-20)

- **Kreismenü** (Rechtsklick, `Umschalt+F10`, „⋯“ in der Leiste): seit 2026-11-25 die acht festen Plätze des
  Interaktionskonzepts, siehe „Interaktionskonzept“ unten (der frühere Prioritätsring mit „Mehr ▸“ entfällt).
- **Text**: Doppelklick auf ein Objekt, die Text-Schaltfläche der Kontextleiste oder „Text …“ im Kreismenü bearbeiten
  den Haupttext (`mainText`, `setMainText`) in der Inline-Eingabe; mehrzeilige Texte öffnen das Textfeld des Inspektors.
- **Einfügen …** im Kreismenü öffnet das Einfügefeld (siehe UI/UX-Runde unten); das gewählte Element startet das
  Einfügen am Mauszeiger auf der Bühne (mit Fangen). In der Seitenleiste des COM-Add-ins sendet sie die vorhandene Bridge-Nachricht `place`, sodass das Element
  auf der Folie platziert wird.
- **Schwarz-Weiß**: Umschalter in der oberen Leiste (und in den Einstellungen) ruft `setMonochrome`; Bühne,
  SVG/PNG-Export und die von `src/office/powerpoint.ts` (oder der COM-Bridge) geschriebenen Zeichenpläne folgen der
  Dokumenteinstellung. Erneutes Klicken stellt die Farben wieder her; Rückgängig funktioniert.
- **Linienstärken** (25.11.2026): eine Auswahl daneben ruft `setLineGroup` (ISO-128-Liniengruppe, Standard 0,5 mm);
  wie Schwarz-Weiß ist sie eine Dokumenteinstellung, die ein Folienmodell in die Seitensitzung mitbringt
  (`importCompact`), die Live-Synchronisierung zeichnet also jede Einheit damit neu. Auf der Browser-Entwicklungsseite
  geprüft (Striche bei 1 mm doppelt so breit).
- **Akzentfarbe** (25.11.2026): eine dritte Auswahl ruft `setAccent` (Voreinstellungen aus `accents`); auf der
  Browser-Entwicklungsseite geprüft (die grüne Ladung eines Batteriespeichers wurde rot, das blaugraue Gehäuse blieb).
- **Kompatibilitätsprüfung** (25.11.2026): ein Knopf in der oberen Leiste öffnet das Panel „Kompatibilität“ mit dem Amt
  (`patentProfiles`) und den Befunden der Vorabprüfung der Seite (`preflight`) – Fehler zuerst, dann Warnungen, selbst
  zu prüfende Punkte und Hinweise, jeweils mit Vorschlag und den Fundstellen als kurze Links; ein Klick auf die Meldung
  eines Befunds wählt sein Element aus. Der Haftungshinweis wird immer angezeigt. Auf der Browser-Entwicklungsseite
  geprüft (hell und dunkel, Wechsel DPMA/EPA/PCT/USPTO, ein Zahnrad außerhalb der Nutzfläche über seinen Befund
  ausgewählt). In PowerPoint (PowerPointApi 1.3) bietet das Panel **Alle Folien**: Die Einheiten jeder Folie werden aus
  ihren Formen gelesen (`src/office/slides.ts`: Name plus `ISK_MODEL`-Tags, sonst die Modellkopie der Präsentation),
  jede Folie wird in einer eigenen Engine-Sitzung geprüft, Befunde lauten „Folie n · …“ und springen zu Folie und Form
  (PowerPointApi 1.5).
- **Mit vorheriger Folie vergleichen** (25.11.2026, nur in PowerPoint): ein Knopf in der oberen Leiste markiert auf der
  ausgewählten Folie, was sich von der vorherigen unterscheidet (Engine `exportCompact` der vorherigen Folie, `diff` auf
  der aktuellen): neu grün, geändert orange, entfernt grau gestrichelt, dazu eine Legende; Formen mit dem Namen
  `ISK_DIFF …`; erneutes Klicken entfernt sie.
- **Folien bereinigen …** (25.11.2026, wie der Prüfen-Befehl des COM-Add-ins): ein Knopf in der oberen Leiste (neben
  Kompatibilitätsprüfung und Vergleich) öffnet ein Formular: *Was* – ausgewählte Elemente (nur mit Auswahl), aktuelle
  Folie, alle Folien; *Diagramme* – normieren (gleiche Verhältnisse, Werte 0…1), zufällig verändern (plausible Werte,
  gleiches Aussehen) oder Werte behalten; *Zusätzlich* – Kategorien umbenennen, Texte ersetzen, Metadaten entfernen
  (standardmäßig an) und ein Zufallsstartwert (leer = ein neuer; ein Startwert für den ganzen Lauf, im Bericht genannt).
  Die Arbeit macht `sanitize` der Engine (`src/ui/cleanlogic.ts` bildet das Formular ab; `Session.sanitize`).
  Ausgewählte Elemente: die Editor-Sitzung (dort ein Rückgängig-Schritt), die auf der Folie verfolgten Einheiten werden
  neu gezeichnet (auch bei ausgeschalteter Live-Synchronisierung). Aktuelle Folie / alle Folien (PowerPointApi 1.5 mit
  Tags): die Einheiten jeder Folie werden so gelesen, wie sie jetzt sind (verschobene oder skalierte Einheiten werden
  zuerst so platziert, `slideSession`), in einer temporären Sitzung bereinigt (`cleanSlide`), und nur die geänderten
  Einheiten werden auf ihrer Folie neu gezeichnet (gleiche Einheiten-IDs, die alten Formen werden also ersetzt; native
  Einheiten gehen direkt auf ihre Folie, ein SVG-Bild braucht die angezeigte Folie, was `writeUnits` erledigt und danach
  wieder die vorherige zeigt). Einheiten, die der Editor hält, übernehmen das bereinigte Modell. Der Bericht ersetzt das
  Formular: Anzahlen, Startwert, höchstens 12 verschiedene Zeilen und der Hinweis, dass native PowerPoint-Diagramme und
  -Tabellen, Notizen, Kommentare und Dokumenteigenschaften nicht verändert werden. **Lücken:** Office.js kann die
  Folienänderungen nicht zu einem Rückgängig-Schritt zusammenfassen (das Formular sagt das und empfiehlt, eine Kopie zu
  bereinigen); außerhalb von PowerPoint (Entwicklungsseite) und in der Seitenleiste des COM-Add-ins (dort gehört die Folie
  dem COM-Add-in, das einen eigenen Befehl hat) bietet das Formular nur die Auswahl und die Editor-Seite.
- **Export-Umfang** (25.11.2026, Vorgabe des Maintainers, wie im COM-Add-in): SVG, PNG und die Fach-Datenexporte
  umfassen immer die Auswahl – ein oder mehrere Elemente, eine Gruppe zählt einmal –, wenn es eine gibt, sonst die
  aktuelle Folie (`src/export.ts`, `resolveExportSource`). Die obere Leiste zeigt den Umfang vor den Export-Knöpfen
  („Export – Auswahl (3)“ / „Export – Folie“), ebenso die Knopf-Tooltips und der Abschnitt „Daten exportieren“ in den
  Einstellungen. Mehrere auf der Folie ausgewählte Einheiten werden jetzt alle in den Editor geladen und gemeinsam
  ausgewählt (`SlideSync.loadSelection`), die Editor-Auswahl ist also die Auswahl der Folie. Ohne Auswahl werden die
  Einheiten der aktuellen Folie in eine temporäre Sitzung gelesen (so, wie sie jetzt auf der Folie sind) und als ganze
  Folie exportiert; eine Folie ohne InventaSketch-Elemente meldet „Auf dieser Folie gibt es nichts zu exportieren“.
  Außerhalb von PowerPoint und in der COM-Seitenleiste wird die Editor-Seite exportiert („Export – Seite“). Dateien heißen
  `inventasketch-selection.*`, `inventasketch-slide-<n>.*` oder `inventasketch.*`.
- **Abgerundete Rechtecke** bekommen ihren Eckenradius als PowerPoint-Anpassung (PowerPointApi 1.10, `adjustments.set`).
- Folien bereinigen, Export-Umfang und das Laden mehrerer ausgewählter Einheiten sind mit Unit-Tests gegen dieselben
  Nachbauten abgedeckt (`test/cleanslides.test.ts`, `test/exportscope.test.ts`, `test/multiselect.test.ts`); **in
  PowerPoint noch nicht ausgeführt**.
- Prüfung über alle Folien, Vergleich und Eckenradius sind mit Unit-Tests gegen ein nachgebautes Office.js-Objektmodell
  und eine Test-Engine abgedeckt (`test/slides.test.ts`); **in PowerPoint noch nicht ausgeführt** (braucht das
  Entwicklungszertifikat). Die Tests fanden, dass die Modellkopie der Präsentation nie zurückgelesen wurde (Tag-Schlüssel
  kommen großgeschrieben zurück); auch für die Auswahl behoben (`mirrorKey`).
- **Skalieren**: zentrierte Teile (Zahnräder, Lager, Räder: `centred`) skalieren standardmäßig um ihre Mitte, mit Strg
  von der Gegenkante; andere Objekte umgekehrt. Eine Beschriftung neben dem Mauszeiger zeigt Größe (oder Faktor) und den
  aktiven Modus mit der Strg-Alternative.
- **Auswahlen als Dropdowns** (wie im COM-Add-in): Jede Auswahl ist ein Dropdown, auch die Aktionsauswahlen der Engine
  für ein Element und für ein gewähltes Teil; siehe „Interaktionskonzept“ unten.
- **Schnell-Anfügen** (wie im COM-Add-in): Ein ausgewähltes Ablaufdiagramm-, UML-, ER-, BPMN- oder Grundform-Element
  (`chain`) zeigt blaue Dreiecke außerhalb seiner Seiten; ein Klick fügt eine verbundene Kopie an (`quickAdd`), Ziehen und
  Loslassen fügt sie an der Ablagestelle ein. Die Kontextleiste beginnt dann mit „Ändern in …“; das öffnet das
  Einfügefeld mit dem, was das neue Element werden kann (`nextItems`, Tippen durchsucht die Bibliothek; die Wahl ruft
  `replaceItem`). An die Leiste wird keine Vorschaureihe mehr angehängt.
- **Kopplung:** Verschieben, Skalieren und Parameteränderungen halten Zahnräder im Eingriff und Räder auf ihren Wellen
  (Engine `Coupling`); mit gedrückter Alt-Taste beim Loslassen wird frei bearbeitet (`couple: false`, `Session.couple`).
- **Verankern** (Einstellungen, standardmäßig an, wie der Menüband-Schalter des COM-Add-ins): Ein auf ein Fangziel
  gezogenes Linienende hängt daran und folgt, wenn das Ziel verschoben wird. Aus: Das Ende rastet ein, bleibt aber stehen.
- **Kreismenü „Beschriftung ▸“** für Linien, Pfeile, Maße und Hinweislinien: Einträge aus den Typparametern
  (Texteingabe, Schalter, Auswahl, Textgröße), wie im COM-Add-in.

## UI/UX-Runde 2026-11-24 (gleichwertig zum COM-Add-in)

Folgt den verbindlichen UX-Regeln im Fahrplan („Plan 2026-11-24“). Reine Regeln in `src/ui/uxlogic.ts` und
`src/ui/insertbox.ts`, mit Unit-Tests in `test/uxround.test.ts`.

- **Leiste nur mit Passendem:** seit 2026-11-25 die Hauptaktion plus bis zu drei Piktogramme plus „⋯“ (siehe
  „Interaktionskonzept“ unten). Spiegeln und Drehen erscheinen nur, wenn `transformEffect` meldet, dass sie die
  Zeichnung ändern (ein Kreis oder ein Zahnrad in Stirnansicht zeigt keins davon); das Kreismenü folgt derselben Regel.
  Piktogramme mit zweisprachigen Tooltips; die Leiste wächst nie mit dem Inhalt.
- **Ein Einfügeweg:** die Einfügen-Schaltfläche der Leiste, „Einfügen …“ im Kreismenü und Strg+Leertaste (auch ohne
  Auswahl) öffnen das Einfügefeld am Mauszeiger. Ohne Eingabe listet es zuerst die zur Auswahl passenden Teile
  (markiert „passt“/„fits“), dann die zuletzt verwendeten; Tippen durchsucht die ganze Bibliothek (`search`). ↑/↓
  bewegen, Enter übernimmt die markierte Zeile, Esc oder ein Klick daneben schließt. Die Zeilen zeigen die
  Bibliotheksvorschauen. Das gewählte Element folgt dem Mauszeiger mit Fangen (Klick platziert, Esc bricht ab). Die
  zuletzt verwendeten Elemente (die letzten 12) stehen in den Benutzereinstellungen (localStorage); Einfügen aus der
  Bibliothek zählt mit.
- **Leiste für Mehrfachauswahl:** Bei zwei oder mehr ausgewählten Elementen (eine Gruppe zählt als eins) zeigt die
  Leiste die Operationen aus `arrangeOptions` als Symbole mit Tooltips – eine Ansicht für alle (Mechanismus bleibt),
  Mitten auf eine Zeile oder Spalte, gleichmäßige Abstände, wie das erste, Bezugszeichen neu nummerieren – jeweils nur,
  wenn sie etwas ändern; ein Klick führt `arrange` aus (ein Rückgängig-Schritt). Stil und Kreismenü bleiben verfügbar.
- **Baugruppe auswählen:** erscheint, wenn die Auswahl mit noch nicht ausgewählten Teilen gekoppelt ist (`assembly`);
  ein Klick wählt alle gekoppelten Objekte aus, die dann die Leiste für Mehrfachauswahl bekommen.
- **Fangbeschriftung:** Beim Ziehen steht die Beschriftung aus `snapMove` („Achse“, „Teilkreis“, „Bolzen 2“) neben
  dem Fangziel (Andockpunkt, eingerasteter Kreis oder Abschnitt).
- **Bahnkurve zeichnen:** Ein Glied eines selbst gebauten Koppelgetriebes mit F = 1 (`trace` liefert eine Bahn) bietet
  „Bahnkurve zeichnen“; es fügt die Koppelkurve des Gliedendes als Bewegungsbahn ein (`kin.path`, bei geschlossener
  Kurve `kin.path.closed`; eine offene Bahn ohne Pfeilspitze), ein Rückgängig-Schritt (`drawTrace`).
- **Bewegungsmodus:** siehe „Bewegungsmodus“ unten (seit 2026-11-25; vorher nur im COM-Add-in).

## Restliche COM-Funktionen (2026-11-24)

Reine Regeln in `src/ui/buildlogic.ts`, mit Unit-Tests in `test/buildlogic.test.ts`.

- **Bau-„+“ an Anschlüssen:** Ein einzeln ausgewähltes Zahnrad, Hohlrad, Welle, E-Maschine, Motor oder Glied zeigt an
  jedem freien Bauanschluss ein blaues „+“ (`buildPorts`): 15 px hinter dem Anschluss in seiner Richtung nach außen mit
  einer dünnen gestrichelten Führungslinie, so weit weiter nach außen geschoben, bis es die Skalier- und Drehgriffe
  freilässt; das „+“ einer freien Nabe sitzt hinter der oberen rechten Ecke, damit sich das Element weiter an seiner
  Mitte ziehen lässt (wie im COM-Add-in). Der Tooltip nennt, was dort andockt. Die Kontextleiste hält die „+“ und den
  Drehgriff frei. Ein Klick öffnet das Einfügefeld am „+“ mit den Teilen, die dort andocken (`buildItems`, mit „passt“
  markiert); die Wahl eines davon dockt es gekoppelt und ausgewählt an (`buildPlace`, ein Rückgängig-Schritt). Tippen
  durchsucht die ganze Bibliothek; was dort nicht andockt, startet das normale Platzieren am Mauszeiger.
- **Wellenende ziehen:** Ziehen des „+“ an einem Wellenende (`stretch`) zeigt das neue Ende auf der Wellenachse an
  (Zeiger eingerastet, dann auf die Achse projiziert) und die Längenänderung neben dem Zeiger; beim Loslassen verlängert
  oder kürzt `buildStretch` die Welle ohne Kopplung, die Teile darauf bleiben an ihrem Platz (ein Rückgängig-Schritt).
- **Live-Vorschau der Kopplung:** Beim Ziehen oder Skalieren der Auswahl werden die Nachbarn, die die Kopplung beim
  Loslassen verschiebt oder skaliert (kämmende Zahnräder, Teile auf einer Welle), blau gezeichnet (`couplingPreview` mit
  `dx`/`dy` oder `sx`/`sy` um den Skalier-Bezugspunkt), gedrosselt auf etwa 15 Aktualisierungen pro Sekunde mit einer
  letzten Aktualisierung für die Endposition. Loslassen, Esc oder Alt (freies Bearbeiten) entfernt sie. Drehen hat keine
  Vorschau.
- **Geisterstellungen:** Ein Glied eines selbst gebauten Koppelgetriebes mit F = 1 (`dof` oder eine Bahnkurve) erhält
  einen Leistenknopf (Symbol `cmd-animate`), der vier gestrichelte graue Kopien der Kette über den Kurbelbereich als eine
  Gruppe einfügt (`drawGhosts`, ein Rückgängig-Schritt).
- **Maß zwischen zwei Teilen:** Bei genau zwei ausgewählten Teilen zeigt die Leiste für Mehrfachauswahl „Maß zwischen
  beiden (Achsabstand)“ aus `arrangeOptions` (`dim`, Symbol `cmd-dimension`, Beschriftung von der Engine); ein Klick fügt
  ein ausgerichtetes Maß ein, das an beiden Achsen andockt (ein Rückgängig-Schritt).

Im Browser geprüft (dev.html, 2026-11-24): fünf „+“ an einem Stirnrad (vier zum Kämmen, eines an der Nabe), ein
Gegenrad über das rechte „+“ angedockt; eine Welle von 120 auf 155 mm verlängert und mit echtem Mausziehen gekürzt,
das Zahnrad darauf blieb an seinem Platz; ein Lager am Wellenende angedockt; ein getippter Suchtreffer startete das
normale Platzieren; die blaue Vorschau des kämmenden Rads beim Ziehen und beim Skalieren seines Partners, beim
Loslassen entfernt und gleich dem Ergebnis; zwölf Geisterglieder als eine Gruppe eingefügt; ein Achsabstandsmaß von
70,7 mm zwischen zwei Zahnrädern.

### Bibliothekssuche (25.11.2026)

Die Bibliothek der Seitenleiste ordnet getippte Suchen mit der Engine-Suche `search` (beide Sprachen, Umlaute = ae/oe/
ue/ss, zuletzt Verwendetes zuerst, Namen mit dem Suchwort am Anfang vor Stichworttreffern) wie das Einfügefeld; die
Kategorie-Chips und Favoriten filtern weiterhin (`rankedItems` in `src/ui/library.ts`).

## Interaktionskonzept (2026-11-25, gleichwertig zum COM-Add-in)

Folgt [interaction-ux.md](../design/interaction-ux.md) (Abschnitte 4–6a) und
[engineering-calculations.md](../design/engineering-calculations.md) Abschnitt 11. Reine Regeln in `src/ui/barlogic.ts`,
`src/ui/pielogic.ts`, `src/ui/handlelayout.ts`, `src/ui/partlogic.ts`, `src/ui/valuecard.ts` und `src/ui/tools.ts`
(Tests `test/barlogic.test.ts`, `test/pie-layout.test.ts`, `test/handlelayout.test.ts`, `test/partlogic.test.ts`).

- **Schwebende Leiste:** derselbe Aufbau für jedes Element. Zuerst die Hauptaktion mit kurzer Beschriftung: „Ändern in …“
  direkt nach einem Schnell-Anfügen, sonst „Werte“ (Erkunden und Ingenieur, Elemente mit Werten), sonst „Daten“
  (Diagramme), sonst „Text“, sonst „Ansicht ▾“, sonst die erste bildhafte Stilauswahl, sonst „Einfügen“. Dann bis zu
  drei Piktogramme in der COM-Reihenfolge: aussagekräftige Typaktionen mit eindeutigem Piktogramm (Schritte behalten
  ihre Beschriftung), die Ansicht, Einfügen, Kurve skizzieren / Koppelkurve / Zwischenlagen / Zerlegen /
  Bewegungsmodus / Baugruppe auswählen, die bildhaften Auswahlen, Auswahlen mit Namen und Menüs, Drehen um 90° und
  Spiegeln, wo sie die Zeichnung ändern. Dann „⋯“ (das Kreismenü). Knöpfe 32 px; die Leiste ist bis 90 px Abstand
  zum Mauszeiger voll sichtbar und blendet bis 420 px aus (UX-Review G1). Eine Mehrfachauswahl behält ihre Gruppen
  (eine Ansicht für alle | ausrichten und verteilen | Baugruppe auswählen) plus „⋯“. Ein gewähltes Teil zeigt seinen
  Namen, seine ersten drei Befehle (Auswahlen als Dropdowns) und „⋯“. Keine Durchschalt-Knöpfe und kein Stil-Knopf mehr
  („Stil ▸“ im Kreismenü hat „Linie und Füllung …“).
- **Auswahlen sind Dropdowns:** ein Dropdown je Auswahl: die Parameterauswahlen (`inspector`), der Linientyp
  (`conversions`), die Linienart, die Ansicht und die Aktionsauswahlen der Engine – jede Aktion mit derselben `choice`
  ist eine Option (Element: Schlüssel `@a.<choice>`, Teil: `@p.<choice>`). Der geschlossene Knopf zeigt
  `choiceBarIcon`, wenn gesetzt, sonst bei `choiceLabelInBar` den Namen der aktuellen Option, sonst ihr Piktogramm
  oder ihr `previewSvg`-Bild, dazu einen kleinen Pfeil; Menüs (`choiceMenu`) zeigen „Bezeichnung ▸“. Die offene Liste
  zeigt jede Option mit Piktogramm oder Bild und Beschriftung, die aktuelle markiert; Esc oder ein Klick daneben
  schließt sie. Die Wahl führt die Option aus (Eingabeoptionen fragen zuerst); eine Menüoption, die ein Teil hinzufügt
  („Füllen bis ▸“), wählt das neue Teil, sodass seine eigenen Auswahlen gleich da sind.
- **Kreismenü:** acht feste Plätze im Uhrzeigersinn ab 12 Uhr: Text …, Bearbeiten ▸, Einfügen …, Duplizieren, Stil ▸,
  Löschen, Ansicht ▸, Anordnen ▸. „Bearbeiten“ enthält die eigenen Befehle des Elements (Werte ▸, Beschriftung ▸,
  Typaktionen, die keine Auswahl abdeckt, Bezugszeichen, Menüauswahlen, Zerlegen, Kurve skizzieren, Bewegungsmodus,
  F = n, Koppelkurve, Zwischenlagen, Teile ▸; bei gewähltem Teil dessen Befehle und „Ganzes Element“); „Stil“ einen
  Eintrag je Zustandsauswahl („Linienart: Gestrichelt ▸“) plus „Linie und Füllung …“; „Ansicht“ die Ansichten;
  „Anordnen“ Drehen in beide Richtungen, Spiegeln, Reihenfolge, Gruppierung und „Verbundene auswählen ▸“. Ein Platz
  ohne Inhalt für das Element bleibt ausgegraut und hält das Menü bei einem Klick offen; ein Platz mit einem Eintrag
  zeigt ihn direkt („Bearbeiten“, „Stil“ und „Anordnen“ behalten ihren Namen). Untermenüs öffnen per Klick und
  beginnen mit „← Zurück“; mehr als elf Einträge gehen in „Mehr ▸“ weiter. Eine Schriftgröße (11,5 px);
  Beschriftungen brechen an Leerzeichen in bis zu drei Zeilen innerhalb ihres Sektors um (geschützte Leerzeichen halten
  „…“ und „▸“ beim Wort); die volle Beschriftung und ein einzeiliger Hinweis stehen unter dem Ring. Die Mitte nennt das
  Element (oder sein Teil oder das offene Untermenü) und schließt das Menü. Der Ring wird verschoben und in einem
  schmalen Bereich verkleinert, sodass er im Bereich bleibt.
- **Auswahl-Schein:** Jedes gewählte Element (eins, mehrere, das Ergebnis von „Verbundene auswählen“) bekommt einen
  weichen blauen Schein entlang seines gezeichneten Umrisses: eine Kopie seiner SVG-Zeichnung in einer Ebene über der
  Zeichnung, durch einen SVG-Filter (Alpha um 3,5 px verbreitert, um 4 px weichgezeichnet, eingefärbt, das Element
  selbst ausgespart). Er liegt unter allen Griffen, zeigt sich auch über Elementen, die über dem gewählten liegen, und
  verschwindet beim Ziehen. Ein gewähltes Teil leuchtet voll, sein Element schwächer. Die gestrichelten Rahmen der
  Mitglieder einer Mehrfachauswahl entfallen.
- **Verbundene auswählen:** „Anordnen ▸ Verbundene auswählen ▸“ listet die zutreffenden Arten (`connectOptions`: Netz,
  bewegt sich mit, folgende Elemente, geschlossene Schleife, Raumwände) mit der Anzahl der Elemente und einem
  einzeiligen Hinweis; das Überfahren eines Eintrags zeigt, was er hinzufügen würde, mit schwächerem Schein, die Wahl
  wählt sie aus. Strg+Doppelklick auf ein Element wählt die sinnvollste Art (`connected` mit Modus `auto`).
- **Teile:** Ein Klick auf eine Kurve, einen Punkt, eine Achse … eines Diagramms wählt dieses Teil sofort, auch wenn
  das Diagramm noch nicht gewählt war (`partAt`); Entf löscht das Teil (`part.delete`) oder blendet es aus, nie das
  Element; ein gelöschter Punkt lässt seine Kurve gewählt.
- **Werte:** In Erkunden und Ingenieur beginnt die Leiste bei Elementen mit Werten (`hasValues`) mit „Werte“
  (Piktogramm x=); „Bearbeiten ▸ Werte ▸“ listet jeden Wert auf jeder Stufe. Die Wertekarte (rechts vom Element) hat
  eine Zeile je Wert: Markierung (✎ eingegeben, = abgeleitet, ○ Vorgabe, ⇄ verbunden, ⚠ im Widerspruch), Symbol,
  Name, Feld (der Parser der Engine: „4k7“, „230 V“, „1,5 kW“), Einheit und × bei einem eingegebenen Wert; unter
  abgeleiteten Werten die Formel Schritt für Schritt (`explain`, Erkunden und Ingenieur; Ingenieur ergänzt die Quelle
  und eine vierte Stelle); eine Statuszeile (✓, was fehlt, Widerspruch) und jeder Widerspruch mit beiden Werten; eine
  abgelehnte Eingabe zeigt ihre Meldung unter ihrer Zeile. Enter oder Verlassen des Felds speichert den Wert
  (`setValue`, ein Rückgängig-Schritt), × löscht ihn (`clearValue`). Darunter: „Rechner …“ und „Werte in der Zeichnung
  zeigen“ (`showValues`). Die Stufe (Schnellskizze, Erkunden, Ingenieur) ist eine Dokumenteinstellung
  (`setValueLevel`), in den Einstellungen und in der Karte.
- **Rechner und Code-Import** (obere Leiste): der Rechner mit Einheiten (`calc`, eine optionale Zieleinheit über
  `convertUnit`; „230 V * 16 A“, „1500 rpm in rad/s“); der Code-Import nimmt eingefügten oder geöffneten matplotlib-,
  pgfplots- oder TikZ-Code (`importCode`: gelesen, nie ausgeführt) und berichtet, was importiert wurde und was nicht.
  `.py`- und `.tex`-Dateien in der Import-Schaltfläche öffnen ihn ebenfalls.
- **Griff-Anordnung:** ein Layout für die Griffe des gewählten Elements (`src/ui/handlelayout.ts`, eine Portierung des
  COM-`HandleLayout`, reine Bildschirmgeometrie): Rahmengriffe, Drehgriff, Punkt-Rauten, Schnell-Anfügen-Dreiecke und
  Wertgriffe bleiben; jedes Bau-„+“ nimmt den ersten freien Platz um seinen bevorzugten Platz (15 px hinter seinem
  Anschluss, bei einer Nabe hinter der oberen rechten Ecke), weiter außen und bis 40° geneigt, mit 3 px Abstand; ein
  „+“, das ausweichen musste, behält eine gestrichelte Verbindung zu seinem Anschluss.
- **Griffe an Werten** (Diagramme): die `dragHandles` des gewählten Elements als blau umrandete Quadrate mit
  Pfeilspitzen in der erlaubten Richtung; die Punktgriffe einer Kurve erscheinen mit der gewählten Kurve oder einem
  ihrer Punkte. Beim Ziehen zeigt eine Vorschau, wo der Wert landet, und seine Beschriftung neben dem Mauszeiger
  (`moveHandle` mit `preview`, etwa 15-mal pro Sekunde); Loslassen ist ein Rückgängig-Schritt, Esc bricht ab, ein Klick
  wählt das Teil des Griffs.

Geprüft mit Unit-Tests, Typprüfung und Produktions-Build; **noch nicht im Browser oder in PowerPoint geprüft**.
Bekannte Unterschiede zum COM-Add-in: noch keine schrittweise Einblendung (jedes „+“ ist sofort sichtbar; kein
Einblenden in Zeigernähe, keine Übersicht mit Umschalt); die Web-Bühne zeichnet keine Schloss-Plaketten, Knoten,
Eckpunkte oder Abzweig-Griffe, daher ordnet das Layout nur „+“ an; Drücken auf eine gewählte Kurve zieht nicht die
ganze Kurve (`byPart`-Griffe); kein ↔-Griff in der Wertekarte und keine ⚠-Plakette in der Zeichnung; eine ungefüllte
geschlossene Form behält innerhalb ihres Umrisses einen schwachen Schein; die Wertestufe ist eine Einstellung des
Engine-Dokuments (COM: ein Präsentations-Tag) und `importCompact` übernimmt sie nicht aus einem Folienmodell.

## Bauen und starten

```powershell
cd hosts/office-web
npm ci
npm test            # vitest (Manifest, Bibliotheksfilter, Kreismenü-Plätze und -Layout, Umbruch, Skalieren, UI/UX-Runde, Bau-„+“-Regeln, Leisten- und Auswahlregeln, Griff-Anordnung)
npm run build       # Engine-Publish + Icons + Typprüfung + Vite-Build + Manifest -> dist/
npm run dev         # Dev-Server auf Port 3000 (https, wenn office-addin-dev-certs installiert sind)
```

`dev.html` startet dieselbe App ohne Office (Engine in WebAssembly, Folienbefehle ausgeblendet) für UI-Arbeit in
jedem Browser.

## Installation für Anwender (ohne Administratorrechte)

`scripts\package-web.ps1` baut `dist\InventaSketch-Web` (und ein ZIP): `App\` (Aufgabenbereich +
WebAssembly-Engine), `Server\InventaSketch.WebHost.exe`, `manifest.xml` für `https://localhost:3217/` (Slidemax-Web
nutzt 3000) und die Installationsskripte mit `README.md` / `README.de.md`. Lokaler Host und Skripte wurden mit
Zustimmung des Rechteinhabers aus Slidemax übernommen und ins Englische übersetzt; Konsolenausgaben, Meldungen und Hilfe
erscheinen auf Deutsch, wenn die Sprache des Benutzers Deutsch ist (Windows-Anzeigesprache bzw. `LC_ALL`/
`LC_MESSAGES`/`LANG` unter macOS/Linux), sonst auf Englisch.

- **Windows:** PowerPoint schließen, `Install web add-in.cmd` doppelklicken, die Zertifikatsabfrage von Windows
  einmal bestätigen. Kopiert nach `%LOCALAPPDATA%\InventaSketch\Web`, erzeugt ein `localhost`-Zertifikat im
  Benutzerspeicher und vertraut ihm, legt einen Autostart-Eintrag an, startet den nur lokal erreichbaren HTTPS-Host und
  registriert das Manifest zum Querladen (`HKCU\Software\Microsoft\Office\16.0\WEF\Developer`). Optionen:
  `--no-autostart`, `--no-manifest`, `--dry-run` (die deutschen Namen `-OhneAutostart`, `-OhneManifest`,
  `-Probelauf` gelten weiterhin). Deinstallation: `Uninstall web add-in.cmd`; Starten/Beenden ohne Neuinstallation:
  `Start web add-in.cmd`, `Stop web add-in.cmd`. `Server\InventaSketch.WebHost.exe --help` listet alle Optionen.
  Solange das COM-Add-in installiert ist, blendet Office das Web-Add-in aus.
- **macOS:** `install.command` (Python 3): kopiert nach `~/Library/Application Support/InventaSketch/Web`,
  vertraut dem Zertifikat im Schlüsselbund, LaunchAgent, Manifest im `wef`-Ordner von PowerPoint. Deinstallation:
  `uninstall.command`.
- **Linux / PowerPoint im Web:** `sh install.sh`, danach `manifest.xml` über *Add-Ins › Meine Add-Ins ›
  Mein Add-In hochladen* einbinden. Deinstallation: `sh uninstall.sh`; `start.sh` / `stop.sh` steuern den Server.
  Optionen von `inventasketch_web.py`: `--no-autostart`, `--dry-run` (deutsche Aliase `--ohne-autostart`,
  `--probelauf`).
- **Eigener HTTPS-Server (IT):** `create-manifest.ps1 -Address https://...` bzw. `sh create-manifest.sh https://...`
  schreibt `manifest-<host>.xml` für den anderswo bereitgestellten Ordner `App`.

Geprüft am 2026-11-20 unter Windows: Paketbau, Probelauf des Installers (Plan inkl. COM-Erkennung), Host mit
temporärem In-Memory-Zertifikat lieferte `taskpane.html` und die Engine per HTTPS aus, wies `../`-Pfade ab, Status und
Stopp funktionierten; Linux-Probelauf des Python-Installers. Noch offen: echte Installation mit Zertifikatsvertrauen
(braucht die Bestätigung des Anwenders), macOS-Lauf, Laden des Add-ins in PowerPoint. Nach der Übersetzung
(2026-11-20): Host-Build ohne Warnungen, Paketbau, Probeläufe des Installers auf Englisch und Deutsch (neue Optionsnamen
und deutsche Aliase), HTTPS-Kurztest (200, `../` 404, fremder Host 400, POST 405), Python-Probeläufe (Linux, simuliertes
macOS) in beiden Sprachen.

## Querladen (Entwicklung)

Windows: einmal `npm run certs` (vertrauenswürdiges localhost-Zertifikat), `npm run dev`, dann den Manifestpfad als
Wert unter `HKCU\Software\Microsoft\Office\16.0\WEF\Developer` eintragen (Name = Add-in-ID) und PowerPoint neu
starten. Für Anwender den oben beschriebenen lokalen Installer verwenden.

## Bewegungsmodus (2026-11-25)

Wie im COM-Add-in („Getriebezüge live durch Ziehen im Bewegungsmodus bewegen“, Ziehen von Koppelgetrieben): Ziehen
bewegt Teile nur so, wie sie sich bewegen können. Reine Regeln in `src/ui/motionlogic.ts`, mit Unit-Tests in
`test/motionlogic.test.ts`.

- **Umschalter:** Die Leiste zeigt „Bewegen: Ziehen dreht Getriebe bzw. Koppelgetriebe“ (Symbol `cmd-motion`, ein
  Umschaltknopf mit `aria-pressed`) zwischen den Baubefehlen und „Zerlegen“, wenn `motionInfo` `linkage` meldet oder
  `gear` mit `coupled` true. Ein einzelnes Zahnrad ohne Kopplung dreht sich nur auf der Stelle, was nichts zeigt, und
  erhält daher keinen Knopf (gleiche Regel wie im COM-Add-in). Der Zustand gilt nur für die Sitzung, wie im COM-Add-in:
  er wird nicht gespeichert, der Aufgabenbereich startet immer im Bearbeitungsmodus. Beim Einschalten erscheint ein
  kurzer Hinweis in der Statuszeile.
- **Griffe im Bewegungsmodus:** Ein ausgewähltes bewegliches Teil zeigt nur, was es bewegt: ein Zahnrad seinen
  gestrichelten Rahmen und ein Kreuz in seiner Mitte, ein Glied seine Gelenke (blau gefüllt). Skalier-, Dreh-,
  Schnellanfüge- und Bau-„+“-Griffe sind ausgeblendet, sodass jedes Ziehen daran eine Bewegung ist. Teile, die sich nicht
  bewegen können, behalten ihre üblichen Griffe und lassen sich wie gewohnt ziehen.
- **Zahnrad drehen:** Ziehen irgendwo im Rahmen des ausgewählten Zahnrads (oder an einem beliebigen gekoppelten
  Zahnrad, das dabei ausgewählt wird) dreht es um seine Mitte (`x`/`y` aus `motionInfo`) um den Winkel, den der Zeiger um
  sie überstreicht, aufsummiert über ±180° und volle Umdrehungen hinweg (im Uhrzeigersinn auf dem Bildschirm positiv);
  Umschalt dreht in 15°-Schritten. Der Winkel steht an der Mitte, mit einem gestrichelten Hebel zum Zeiger.
- **Glied ziehen:** Ziehen an einem Gelenkgriff des ausgewählten Glieds zieht dieses Gelenk; Ziehen an anderer Stelle
  eines Glieds zieht sein nächstgelegenes Gelenk. Der gezogene Punkt und eine gestrichelte Linie von seinem Start werden
  angezeigt.
- **Vorschau und Loslassen:** Während des Ziehens wird die `motionPreview` der Engine (der gedrehte Zug oder die bewegte
  Kette) etwa 15-mal pro Sekunde in der Vorschauebene gezeichnet (gleiche Drosselung wie die Kopplungsvorschau, letzte
  Aktualisierung für die Endposition) und die aktuelle Zeichnung der bewegten Objekte abgeblendet; die Kontextleiste
  verschwindet, sobald sich das Ziehen bewegt. Loslassen wendet `motionTurn` bzw. `motionDrag` einmal an (ein
  Rückgängig-Schritt: „Turn“ / „Move linkage“); ein Klick ohne Ziehen ändert nichts; Esc bricht ab und zeigt die Leiste
  wieder. Bei ausgeschaltetem Bewegungsmodus bearbeitet Ziehen wie bisher.

Im Browser geprüft (dev.html, 2026-11-25): ein Stirnrad mit einem über sein Bau-„+“ angedockten Gegenrad: der
Bewegungsknopf erscheint (nicht bei einem einzelnen Zahnrad), echtes Mausziehen über einen Viertelkreis drehte das Rad
um +90,4° und das Gegenrad von -10° auf -100,4°, beide Mitten unverändert, ein Rückgängig-Schritt „Turn“; bei
ausgeschaltetem Bewegungsmodus verschob dasselbe Ziehen das Rad. Ein zerlegtes `kin.fourbar` (F = 1): das Kurbelgelenk um
30° um seinen Drehpunkt gezogen, alle Gliedlängen blieben gleich (9,124 / 27,371 / 22,809 mm), die Gestellpunkte an
ihrem Platz, ein Rückgängig-Schritt „Move linkage“; die Vorschau zeigte Kurbel und Koppel bewegt, ihre Originale
abgeblendet; ein Ziehen an der Koppel ohne Auswahl wählte sie aus, Esc brach es ohne Änderung ab. Bekannte Lücke
(Engine): `motionPreview` lässt ein Glied aus, das sich nur über ein angedocktes Gelenk bewegt (die Schwinge des
Viergelenks), und jede Vorschau rechnet vom Dokumentzustand statt von der letzten Vorschau aus, sodass ein langes Ziehen
eines Koppelgetriebes in der Vorschau in die andere Montagelage springen kann (das COM-Add-in rechnet von der letzten
Lage aus).

## Stand (2026-11-20)

| Funktion | Stand |
|---|---|
| Engine in WebAssembly, Katalog, Einfügen am Mauszeiger, Suche | im Browser geprüft (dev.html) |
| Bühneninteraktion (Verschieben, Skalieren, Fangen, Kreismenü, schwebende Leiste) | umgesetzt, Browserprüfung teilweise |
| Kreismenü mit „Mehr ▸“/„Einfügen ▸“, Textbearbeitung, Schwarz-Weiß, Skalieren um die Mitte | im Browser geprüft (dev.html, 2026-11-20) |
| UI/UX-Runde: Spiegeln/Drehen nur mit Wirkung, Einfügefeld, Leiste für Mehrfachauswahl, Baugruppe auswählen, Fangbeschriftung, Bahnkurve zeichnen | im Browser geprüft (dev.html, 2026-11-24); **noch nicht in PowerPoint getestet** |
| Bau-„+“ (andocken, Welle ziehen), Live-Vorschau der Kopplung, Geisterstellungen, Maß zwischen zwei Teilen | im Browser geprüft (dev.html, 2026-11-24); **noch nicht in PowerPoint getestet** |
| Bewegungsmodus (Getriebezüge drehen, Koppelgetriebe ziehen) | im Browser geprüft (dev.html, 2026-11-25); **noch nicht in PowerPoint getestet** |
| Interaktionskonzept: Leiste, Auswahl-Dropdowns, Kreismenü mit acht Plätzen, Schein, Verbundene auswählen, Teile, Werte, Rechner, Code-Import, Griff-Anordnung, Wertgriffe | Unit-Tests, Typprüfung und Build (2026-11-25); **noch nicht im Browser oder in PowerPoint geprüft** |
| Schreiben nativer Formen in PowerPoint | umgesetzt, **noch nicht in PowerPoint getestet** |
| Lokaler Installer wie beim COM-Add-in (ohne Admin) | umgesetzt, Probelauf geprüft; echte Installation offen |
| Webspace-Paket (Downloadseite, Manifest für eigene URL) | umgesetzt; über lokales HTTPS geprüft, echter Webspace offen |

Grenzen von Office.js (siehe [Host-Fähigkeiten](capability-matrix.md)): keine Freiformen, Pfeilspitzen oder
Musterfüllungen auf Formen; kein Overlay auf der Folie; iPad nur PowerPointApi 1.1.

## Webspace-Paket

`dist/InventaSketch-Webspace` (und `dist/InventaSketch-Webspace-<Version>.zip`, das den Ordner enthält) stellt das
Web-Add-in von einem beliebigen HTTPS-Webspace bereit, ohne Server auf dem Rechner des Anwenders. Bau mit
`scripts/package-webspace.ps1` (Windows) bzw. `scripts/package-webspace.sh` (Linux/macOS); `scripts/build.ps1` und
`scripts/build.sh` rufen es nach dem Web- und dem COM-Paket auf und warnen nur, wenn der Web-Build fehlt. Die Arbeit
erledigt `hosts/office-web/webspace/package.mjs` (`npm run webspace`).

Inhalt:

- das gebaute Web-Add-in (`hosts/office-web/dist` ohne `dev.html`, Quelltextkarten und localhost-Manifest), samt
  vorkomprimierter Engine-Dateien (`_framework/*.br`, `*.gz`);
- `index.html` mit `setup.js`: die Einrichtungsseite. Sie nimmt den Ordner ihrer eigenen Adresse (`location`, ohne
  Dateiname, Abfrage und Anker) als Adresse des Add-ins, prüft sie nach den Regeln von `create-manifest.ps1` (nur HTTPS,
  ohne Abfrage oder Sonderzeichen), zeigt sie an und bietet das Manifest als `manifest-<host>[-<port>].xml` auf Knopfdruck
  an. Dazu erklärt sie "Mein Add-In hochladen" für PowerPoint unter Windows, auf dem Mac und im Web, den `wef`-Ordner auf
  dem Mac und die Registry unter Windows (`HKCU\Software\Microsoft\Office\16.0\WEF\Developer`) mit kopierbaren Befehlen
  für den Fall, dass das Hochladen fehlt, den Katalog im freigegebenen Ordner und die zentrale Bereitstellung durch die IT.
  Texte sind deutsch, wenn die Browsersprache mit `de` beginnt, sonst englisch; ein Knopf wechselt die Sprache;
- `manifest-template.js`, erzeugt aus `scripts/manifest.mjs` (`manifestTemplateModule`): Vorlage des Manifests und
  `fillManifest`, damit Seite und `node scripts/manifest.mjs <url>` für dieselbe Adresse dasselbe XML erzeugen
  (`test/webspace.test.ts`);
- `.htaccess` (Apache) und `web.config` (IIS): MIME-Typen für `.wasm`, `.dll`, `.dat`, `.blat`, `.json`, `.js`, `.br`,
  `.gz`; kein Verzeichnislisting; `X-Robots-Tag: noindex`, `Referrer-Policy: no-referrer`, `Cache-Control: no-cache` (ein
  neuer Upload kommt sofort an); Crawler, Vorschau-Dienste und Werkzeuge werden anhand des User-Agents abgewiesen. Apache
  liefert die `.br`-/`.gz`-Kopien mit `Content-Encoding` aus, wenn der Browser sie annimmt (braucht `mod_rewrite` und
  `mod_headers`, sonst gehen die Originale raus). IIS liefert die Originale und komprimiert selbst; vorkomprimierte
  Dateien bräuchten das optionale URL-Rewrite-Modul, dessen Fehlen den ganzen Ordner lahmlegt;
- `robots.txt` (`Disallow: /`; Crawler lesen sie nur im Wurzelverzeichnis eines Hosts, im Unterordner wirken der Header
  und das robots-Meta-Tag);
- `version.json` (Versionszeile auf der Seite) und `downloads.json` mit `downloads/`: die übrigen Fassungen, die neben dem
  Paket in `dist` liegen: `InventaSketch-AddIn-<Version>.zip` (COM-Add-in) und
  `InventaSketch-LibreOffice-<Version>[-<rid>].oxt`. Aufgeführt werden nur vorhandene Dateien, nie erwartete; ohne sie
  meldet die Seite, dass es keine weiteren Fassungen gibt.

Verwendung: den Ordner mit allen Dateien hochladen (FTP-Programme zeigen `.htaccess` erst mit eingeschalteten versteckten
Dateien), in einen Ordner mit nicht erratbarem Namen, z. B. `https://example.com/isk-7k2q9x/`. Voraussetzungen: HTTPS mit
gültigem Zertifikat und kein Passwortschutz (Office-Add-ins können sich nicht anmelden). Die Adresse im Browser öffnen,
das Manifest herunterladen und in PowerPoint hochladen (Start > Add-Ins > Weitere Add-Ins > Meine Add-Ins > Mein Add-In
hochladen). Präsentationsdaten gehen nicht an den Webspace. Aktualisieren: den neuen Ordner hochladen und den alten Inhalt
ersetzen; ein neues Manifest ist nur nötig, wenn sich das Manifest geändert hat.

Herkunft: Einrichtungsseite, Servereinstellungen und Paketbau wurden aus Slidemax-Webspace (gleicher Rechteinhaber)
übernommen und ins Englische übersetzt.

Geprüft am 2026-11-20 unter Windows: Paketbau (111 Dateien, COM-Add-in-ZIP aufgeführt); der Ordner über einen lokalen
statischen Server in einem Unterordner per HTTPS (temporäres selbstsigniertes Zertifikat) und HTTP ausgeliefert; die Seite
in Edge (headless) auf Deutsch und Englisch geladen: Adresse `https://localhost:<Port>/isk-test`, erzeugtes Manifest
wohlgeformtes XML mit dieser Adresse und identisch mit `scripts/manifest.mjs`; über HTTP lehnt die Seite die Adresse ab
und sperrt den Knopf. Noch nicht geprüft: `.htaccess` auf einem echten Apache und `web.config` unter IIS, Laden des
Add-ins in PowerPoint von einem Webspace.
