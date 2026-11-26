# Technische Zeichnungen: Modul Bezugszeichen

Bibliothek: `src/InventaSketch.Modules.Patent` (Modul-Id `patent`) · Tests: `tests/InventaSketch.Modules.Patent.Tests` ·
Englische Fassung: [../../en/modules/reference-numerals.md](../../en/modules/reference-numerals.md)

Das Modul bereitet technische Zeichnungen für die Einreichung bei einem Amt vor: **Bezugszeichen** mit Bezugslinien,
die **Bezugszeichenliste** (Bezugszeichen und Teilename), **automatische Nummerierung aller Teile**, **Varianten** von
Bezugszeichen (10a, 10b, 10′), **Montageachsen** für Explosionsdarstellungen, **Oberflächenschattierung**, eine
Zeichnungsblatt-Hilfslinie, Blattnummern, Amtsprofile, eine **Kompatibilitätsprüfung (Preflight)** und den
Schwarzweiß-Export. Die geprüften Amtsvorschriften stehen mit Quellen in
[../../en/research/drawing-rules.md](../../en/research/drawing-rules.md). Ein Preflight ohne Befund ist eine Zeichenhilfe,
keine rechtliche Bestätigung.

## Konventionen

- **Quick Sketch zuerst:** Jedes Bibliothekselement lässt sich ohne Parameter einfügen. Skalieren ändert nur `Size`.
- **Bezugszeichen** sind Punktobjekte: Punkt 0 ist das Ende am Teil (es rastet am Teil ein und folgt ihm), der letzte
  Punkt ist das Bezugszeichen. Das Bezugszeichen selbst ist `Ref` (bevorzugt), sonst der Text, sonst der Parameter
  `numeral`. Unter Profilen, die eingeschlossene Zeichen verbieten (EPA, PCT, USPTO, common-safe), werden Klammern und
  Anführungszeichen nie gezeichnet.
- **Teile** sind die Elemente, die in der Stückliste zählen (`PartsList.IsPart`): Beschriftung, Bezugszeichen,
  Blattmarken, Kartensymbole, Diagrammformen und Charts sind keine Teile.
- **Linienarten:** Bezugslinien schmal durchgezogen; Montageachsen schmal gestrichelt (oder als Strichpunkt-Mittellinie);
  Schattierung schmale Linien.
- Alle Operationen sind reine Dokumentänderungen; jede Aktion liefert ein neues Dokument, Hosts zeichnen sie daher als
  **einen Rückgängig-Schritt** auf.

## Objekttypen

| Typ-Id | Element | Parameter (alle optional) |
|---|---|---|
| `patent.numeral` | Bezugszeichen mit Bezugslinie (gerade, gebogen, wellig; Pfeil für eine ganze Baugruppe; unterstrichenes Flächen-Bezugszeichen ohne Bezugslinie) | `numeral` (10), `label` (Teilename), `underline`, `curve`, `end`, `bend`, `textSize` |
| `patent.exploded.axis` | Montageachse einer Explosionsdarstellung: schmale Linie in der Lücke zwischen zwei auseinandergezogenen Teilen; beide Enden rasten an den Teilen ein | `form` (`dashed` Standard, `chain` = Strichpunkt-Mittellinie) |
| `patent.shading` | Oberflächenschattierung über einem Teil: schmale Linien mit Abstand statt Grautönen, Licht von links oben (Linien verdichten sich rechts unten). Formen: Zylinder (parallele Linien, zum Rand hin dichter), Kegel (Linien fächern von der Spitze), Kugel (konzentrische Bögen), ebene Fläche (gleichmäßige Linien). Box auf die Fläche skalieren und drehen; spiegeln für Licht von der anderen Seite. Kein Teil. | `form`, `lines` (5, 2–20) |
| `patent.sheet` | Zeichnungsblatt-Rahmen und Nutzfläche eines Profils (Hilfslinie, nie exportiert, vom Preflight ignoriert) | `profile` |
| `patent.pagenumber` | Blattnummer „n/N“, aus der Seitenposition berechnet | `auto`, `textSize` |

Die Schattierung folgt den Zeilen US-15 (37 CFR 1.84(m): schmale Linien mit Abstand, Licht von links oben unter 45°) und
EP-20 (Richtlinien A-IX 7.2: Schattierung zulässig, wenn sie dem Verständnis dient) des Recherchedokuments; Voll- und
Grauflächen bleiben Preflight-Befunde.

## Aktionen

| Aktion | An | Wirkung |
|---|---|---|
| **Alle Teile nummerieren ▸ 10, 20, 30 … / 1, 2, 3 …** (`patent.numerals.auto=tens` / `=ones`) | Bezugszeichen, Blattrahmen, Blattnummer | Nummeriert alle Teile der Seite ohne Bezugszeichen in Leserichtung (Zeilen von oben nach unten, in der Zeile von links nach rechts; ein Teil gehört zu einer Zeile, wenn seine senkrechte Mitte innerhalb der Höhe des obersten Teils der Zeile liegt). Jedes Teil erhält ein Bezugszeichen mit Bezugslinie von innerhalb des Teils bis über seine äußere obere Ecke hinaus; das Bezugszeichen wird am Teil (`Ref`, die Stückliste zeigt es) und in der Bezugszeichenliste mit dem Teilenamen eingetragen (mit Zähler eindeutig gemacht, „Block 2“, wenn eine andere Nummer den Namen schon nutzt). Teile mit Bezugszeichen werden übersprungen; ein Teil, das selbst schon ein Bezugszeichen trägt, behält es und erhält nur die Bezugslinie. Gleiche Teile (gleicher Typ, Text und Parameter) teilen sich standardmäßig ein Bezugszeichen (`RepeatedParts.Shared`); die API bietet auch Varianten 10a, 10b (`Suffixed`) oder je ein eigenes Bezugszeichen (`Separate`). Ausgegraut, wenn alle Teile nummeriert sind. |
| **Variante ▸ 10 / 10a / 10b / 10c / 10d / 10′ / 10″** (`patent.numeral.suffix=…`) | Bezugszeichen | Setzt die Varianten-Endung (wiederholte oder abgewandelte Teile). Das Teil, auf das es zeigt, folgt; eine neue Variante erhält in der Liste den Teilenamen ihrer Nummer. Umnummerieren behält Endungen (10a → 20a). |
| **Montageachse zum nächsten Teil** (`patent.numeral.axis`) | Bezugszeichen an einem Teil | Fügt eine Montageachse vom nummerierten Teil zum nächstgelegenen, noch nicht verbundenen Teil ein, auf der Linie durch beide Mitten, von Rand zu gegenüberliegendem Rand (Mitte zu Mitte, wenn sich die Teile überlappen). Erneut ausführen für das nächste Teil. |
| Fläche ▸ Zylinder / Kegel / Kugel / ebene Fläche; Mehr Linien / Weniger Linien | Schattierung | Form und Linienzahl der Schattierung. |
| Linie ▸ gestrichelt / Strichpunkt | Montageachse | Linienart der Achse. |

API (`ReferenceNumerals`, `PartNumbering`, `ExplodedViews`): `NextFree`, `Insert`, `Rename`, `Renumber`, `Apply`, `Sync`,
`List`, `Groups`, `Export`, `Check`, `SplitSuffix`, `WithSuffix`, `SetSuffix`, `NumberedParts`, `PartKind`,
`PartsInReadingOrder`, `NumberParts`, `PageCommands`, `AddAxis`, `NearestPart`.

**Alle Teile nummerieren ohne ausgewähltes Bezugszeichen** (Runde 4, 26.11.2026): dasselbe Menü ist ein Befehl der ganzen
Seite (`PartNumbering.PageCommands`, Engine `pageActions` / `applyPageAction`, dieselben Ids `patent.numerals.auto=tens`
/ `=ones`). Es wird nur auf einer Seite mit Teilen angeboten und ist ausgegraut, sobald jedes Teil ein Bezugszeichen hat.
Hosts: COM-Add-in *Mehr ▸ Alle Teile nummerieren ▸* (die Folie; neue Bezugszeichen schließen an die der anderen Folien
an), Web-Add-in eine Schaltfläche der oberen Leiste mit dem Umnummerieren-Piktogramm, solange einem Teil ein
Bezugszeichen fehlt, LibreOffice Klappliste *Alle Teile nummerieren ▸* im Bereich *Zeichnung, Rechner und Code* (siehe
die Host-Dokumente).

**Über Seiten und Folien hinweg** (Runde 5, 26.11.2026): Alle drei Hosts halten jede Folie oder Seite in einem eigenen
Modell, die Nummerierung einer Seite begann daher wieder bei 10. Die Hosts übergeben jetzt die Bezugszeichen der anderen
Folien/Seiten (`OtherPage`: Seitenbezeichnung und `OtherNumeral` = Bezugszeichen, Teilename, Teileart) als
`NumberingOptions.Elsewhere` / `PageCommandContext.Elsewhere` (Engine-Anfragefeld `elsewhere`, Zusammenfassungen aus der
Engine-Methode `numeralsOf` über die Einheitenmodelle der anderen Seiten, im COM-Add-in `OtherPage.Of`):

- Bezugszeichen und Listeneinträge der anderen Seiten zählen als belegt, `NextFree` liefert also die erste im ganzen
  Dokument freie Nummer (Lücken werden wie auf einer einzelnen Seite gefüllt: ist anderswo nur 40 belegt, erhält ein neues
  Teil 10);
- ein Teil derselben Art wie ein auf einer anderen Seite nummeriertes Teil übernimmt dessen Bezugszeichen und Namen
  (gemeinsame Nummerierung; mit Varianten erhält es die nächste Variante dieser Nummer). „Dieselbe Art“ ist die schon auf
  einer Seite genutzte Identität der Stückliste (`ReferenceNumerals.PartKind`: Typ, Text und alle Parameter – die
  Bezeichnung `ref`/`tag`/`name` eingeschlossen; Größe, Lage und Ansicht zählen nicht);
- Namen bleiben über Seiten hinweg eindeutig („Block 2“, wenn eine andere Nummer auf einer anderen Seite „Block“ nutzt);
- der Seite werden nur die Listeneinträge der hier vergebenen Bezugszeichen hinzugefügt; die Einträge der anderen Seiten
  bleiben, wo sie sind.

Das eigene Menü *Alle Teile nummerieren ▸* eines Bezugszeichens verhält sich genauso: COM leitet es an den Folienbefehl
weiter, Web und LibreOffice übergeben `elsewhere` mit `applyAction`, und die Engine führt dann den Seitenbefehl aus.

## Bezugszeichenliste

`ReferenceNumerals.Export` schreibt die Liste als Text, eine Zeile je Teil mit gruppierten Varianten („10, 10a, 10′
housing“); eine Variante mit eigenem Namen bleibt eine eigene Zeile unter ihrer Nummer. Die CSV-Form behält eine Zeile je
Bezugszeichen (`numeral,label`), Varianten unter ihrer Nummer. Der Fachexport „Bezugszeichenliste (Text)“ nutzt die
Textform.

## Preflight-Befunde zu Bezugszeichen

Neben den Prüfungen zu Blatt, Farbe, Text und Figuren (Recherchedokument, Abschnitt 5) meldet der Preflight:

| Regel | Schwere | Befund | Quelle |
|---|---|---|---|
| `PF-LEAD-FREE` | Info | Bezugslinie nicht verbunden, ihr Ende liegt aber auf einem Element | P11: EP-16, US-20 |
| `PF-LEAD-TARGET` | Warnung | **Bezugszeichen zeigt ins Leere**: die Bezugslinie endet auf keinem Element oder ist mit einem Element verbunden, das es nicht mehr gibt | P11: EP-16, US-20 |
| `PF-REF-CONFLICT` | Fehler | Ein Bezugszeichen für verschieden benannte Teile | P19 |
| `PF-REF-DUPLICATE` | Fehler | **Ein Bezugszeichen an Teilen verschiedener Art** (anderer Typ, Text oder Parameter; gleiche Kopien dürfen ein Bezugszeichen teilen) | P19: DE-12, EP-15, PCT-13, US-19 |
| `PF-REF-LABEL` | Warnung | Ein Teilename mit verschiedenen Nummern (Varianten einer Nummer sind zulässig); **über Seiten hinweg** auch dieselbe Teileart (`PartKind`), deren Bezugszeichen auf einer Seite keine Nummer mit ihren Bezugszeichen auf einer anderen Seite teilen (`ReferenceIssueKind.SamePartDifferentNumerals`) – außer beide Seiten benennen die Teile verschieden (ein schraffiertes Gehäuse und ein schraffierter Deckel sind eine Art, aber verschiedene Teile); absichtlich getrennt nummerierte Teile (10 und 20 für zwei gleiche Zahnräder) sind in Ordnung, solange die andere Seite eine ihrer Nummern nutzt | P19 |
| `PF-REF-DUPLICATE` (über Seiten) | Fehler | Ein Bezugszeichen, das auf einer anderen, getrennt gehaltenen Folie/Seite ein anderes Teil bezeichnet („Bezugszeichen 10 bezeichnet auf Folie 3 ein anderes Teil (Welle)“) | P19: DE-12, EP-15, PCT-13, US-19 |
| `PF-REF-MISSING` / `PF-REF-UNUSED` | Fehler / Warnung | Bezugszeichen nicht in der Liste / Listeneintrag nie gezeichnet | P19 |
| `PF-PART-UNNUMBERED` | Info | **Teil ohne Bezugszeichen** (kein Bezugszeichen zeigt darauf und es trägt keines; ein Bezugszeichen mit Pfeil deckt seine ganze Gruppe ab). Zeichenhilfe für jedes Profil: Keine Amtsvorschrift verlangt ein Bezugszeichen an jedem Teil, der Befund zitiert daher keine. | Zeichenhilfe |

Die Prüfungen über Seiten hinweg (`ReferenceNumerals.CheckAcrossPages`) vergleichen die Seiten eines Dokuments
untereinander und über `PreflightOptions.Elsewhere` (Engine `preflight` mit `elsewhere` und `pageName` „slide“/„page“)
jede geprüfte Folie oder Seite mit den anderen, die der Host getrennt hält. Die Prüfung „alle Folien/Seiten“ jedes Hosts
übergibt sie; ein Befund wird auf jeder beteiligten Seite gemeldet und zeigt auf deren Teile und Bezugszeichen.

## Hinweise zu den Hosts

- Bezugszeichen, Montageachsen und Schattierung sind einfache Linien, Polylinien und Text; jeder Host zeichnet sie als
  native Formen.
- „Alle Teile nummerieren“ und „Montageachse zum nächsten Teil“ sind Dokumentaktionen (`ContextAction.ApplyInDocument`),
  die Objekte hinzufügen; Hosts, die Dokumentaktionen ausführen, wenden sie als eine Änderung an. Sie werden an einem
  Bezugszeichen, am Blattrahmen und an der Blattnummer angeboten; ohne ausgewähltes Objekt führt sie der Seitenbefehl
  oben aus (Engine `applyPageAction`).

## Offene Punkte

- Das Web-Add-in in der Seitenleiste des COM-Add-ins hat keinen eigenen Folienzugriff: Nummerierung und Prüfung sehen dort
  nur die gezeigte Folie (das COM-Add-in selbst mit *Mehr ▸ Alle Teile nummerieren* und seiner Kompatibilitätsprüfung
  sieht alle Folien).
- Die Platzierung der Bezugszeichen meidet nur die Blattränder, noch nicht andere Bezugszeichen oder Kreuzungen von
  Bezugslinien (der Preflight meldet Kreuzungen).
- Klammern um auseinandergezogene Teile für das US-Profil (Recherche P20) werden noch nicht gezeichnet oder geprüft.
