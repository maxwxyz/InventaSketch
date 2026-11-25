"""User-facing texts of the extension, English and German (CLAUDE.md: German if the UI language starts with "de",
otherwise English). Library names come from the engine catalog in the same language."""

TEXTS = {
    "title": ("InventaSketch", "InventaSketch"),
    "search": ("Search", "Suchen"),
    "category": ("Category", "Kategorie"),
    "allCategories": ("All categories", "Alle Kategorien"),
    "insert": ("Insert", "Einfügen"),
    "items": ("%d elements", "%d Elemente"),
    "suggestions": ("Suggestions (fitting, recent)", "Vorschläge (passend, zuletzt)"),
    "fitsRow": ("%s – fits", "%s – passt"),
    "searchHint": ("Type to search the whole library; parts that fit the selection come first.",
                   "Tippen durchsucht die ganze Bibliothek; Teile, die zur Auswahl passen, stehen vorn."),
    "noUnit": ("No InventaSketch unit selected.", "Keine InventaSketch-Einheit ausgewählt."),
    "unitsSelected": ("%d InventaSketch units selected", "%d InventaSketch-Einheiten ausgewählt"),
    "editSelected": ("Refresh selection", "Auswahl aktualisieren"),
    "view": ("View", "Ansicht"),
    "redraw": ("Redraw from model", "Aus dem Modell neu zeichnen"),
    "dofInfo": ("F = %d (%d links, %d joints)", "F = %d (%d Glieder, %d Gelenke)"),
    "actTrace": ("Draw path", "Bahn zeichnen"),
    "tipTrace": ("Draw the path of this link's end (coupler curve)", "Bahnkurve des Gliedendes zeichnen (Koppelkurve)"),
    "actGhosts": ("Ghost positions", "Zwischenlagen"),
    "tipGhosts": ("Ghost positions of the linkage", "Zwischenlagen des Koppelgetriebes"),
    "actAssembly": ("Select assembly", "Baugruppe wählen"),
    "tipAssembly": ("Select the assembly (all coupled parts)", "Baugruppe auswählen (alle gekoppelten Teile)"),
    "actExplode": ("Explode", "Zerlegen"),
    "tipExplode": ("Explode into parts", "In Einzelteile zerlegen"),
    "actMirrorH": ("Mirror ↔", "Spiegeln ↔"),
    "tipMirrorH": ("Mirror horizontally", "Horizontal spiegeln"),
    "actMirrorV": ("Mirror ↕", "Spiegeln ↕"),
    "tipMirrorV": ("Mirror vertically", "Vertikal spiegeln"),
    "actRotate": ("Rotate 90°", "Drehen 90°"),
    "tipRotate": ("Rotate 90 degrees", "Um 90 Grad drehen"),
    "build": ("Build on (+)", "Anbauen (+)"),
    "buildPlace": ("Build", "Anbauen"),
    "tipBuild": ("Dock the chosen part at this port, coupled", "Gewähltes Teil an dieser Stelle gekoppelt anbauen"),
    "noBuildItems": ("Nothing docks here", "Hier passt nichts"),
    "turn": ("Turn the gear train by (°)", "Getriebe drehen um (°)"),
    "turnRun": ("Turn", "Drehen"),
    "tipTurn": ("Turns the gear with its whole train (ratios kept)",
                "Dreht das Rad mit dem ganzen Getriebe (Übersetzungen bleiben)"),
    "badAngle": ("Please enter an angle in degrees, e.g. 30 or -45.",
                 "Bitte einen Winkel in Grad eingeben, z. B. 30 oder -45."),
    "noHits": ("No element found for '%s'.", "Kein Element für '%s' gefunden."),
    "portMesh": ("Mesh", "Eingriff"),
    "portInnerMesh": ("Inner mesh", "Innenverzahnung"),
    "portAxisEnd": ("Shaft end", "Wellenende"),
    "portHub": ("Hub", "Nabe"),
    "portPin": ("Planet pin", "Planetenbolzen"),
    "portBevel": ("Cone apex", "Kegelspitze"),
    "portJoint": ("Joint", "Gelenk"),
    "portMount": ("Flange (tool)", "Flansch (Werkzeug)"),
    "dirRight": ("right", "rechts"),
    "dirDownRight": ("bottom right", "rechts unten"),
    "dirDown": ("bottom", "unten"),
    "dirDownLeft": ("bottom left", "links unten"),
    "dirLeft": ("left", "links"),
    "dirUpLeft": ("top left", "links oben"),
    "dirUp": ("top", "oben"),
    "dirUpRight": ("top right", "rechts oben"),
    "noPanel": ("The InventaSketch sidebar panel is not open.", "Die InventaSketch-Seitenleiste ist nicht geöffnet."),
    "undoAction": ("InventaSketch: %s", "InventaSketch: %s"),
    "arrangeLabel": ("Arrange", "Anordnen"),
    "loading": ("Loading library ...", "Bibliothek wird geladen ..."),
    "undoInsert": ("InventaSketch: insert %s", "InventaSketch: %s einfügen"),
    "undoEdit": ("InventaSketch: edit %s", "InventaSketch: %s bearbeiten"),
    "undoSnap": ("Move and snap", "Verschieben und einrasten"),
    "ctxViews": ("Switch view", "Ansicht wechseln"),
    "ctxSidebar": ("More in the InventaSketch sidebar…", "Mehr in der InventaSketch-Seitenleiste …"),
    "notSupported": ("InventaSketch works in Impress, Draw and Calc.",
                     "InventaSketch funktioniert in Impress, Draw und Calc."),
    "noModel": ("The selected shape has no InventaSketch model.",
                "Die ausgewählte Form hat kein InventaSketch-Modell."),
    "engineFailed": ("The InventaSketch engine reported an error:\n%s",
                     "Die InventaSketch-Engine meldet einen Fehler:\n%s"),
    "noRuntime": ("InventaSketch needs the .NET 10 runtime (or newer), which was not found.\n\n"
                  "Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and restart LibreOffice, "
                  "or install the InventaSketch package for your platform, which includes the runtime.\n\n"
                  "Searched: %s",
                  "InventaSketch braucht die .NET-10-Laufzeit (oder neuer), die nicht gefunden wurde.\n\n"
                  "Bitte von https://dotnet.microsoft.com/download/dotnet/10.0 installieren und LibreOffice neu "
                  "starten oder das InventaSketch-Paket für Ihre Plattform installieren, das die Laufzeit enthält.\n\n"
                  "Gesucht: %s"),
    "noEngine": ("The InventaSketch engine is missing in the extension folder (%s). Please reinstall the extension.",
                 "Die InventaSketch-Engine fehlt im Ordner der Erweiterung (%s). Bitte die Erweiterung neu "
                 "installieren."),
    "engineTimeout": ("The InventaSketch engine did not answer '%s' within %d s and was restarted.",
                      "Die InventaSketch-Engine hat auf '%s' nicht innerhalb von %d s geantwortet und wurde neu "
                      "gestartet."),
    "engineEnded": ("The InventaSketch engine ended unexpectedly.%s",
                    "Die InventaSketch-Engine wurde unerwartet beendet.%s"),
    "about": ("InventaSketch %s for LibreOffice\n\nTechnical sketches, patent drawings and diagrams as native, "
              "editable shapes.\n\nEngine: %s\nLicence: Apache License 2.0\n%s",
              "InventaSketch %s für LibreOffice\n\nTechnische Skizzen, Patentzeichnungen und Diagramme als native, "
              "bearbeitbare Formen.\n\nEngine: %s\nLizenz: Apache License 2.0\n%s"),
    "pcTitle": ("Patent check (drawings)", "Patentprüfung (Zeichnungen)"),
    "pcOffice": ("Office:", "Amt:"),
    "pcCheck": ("Check", "Prüfen"),
    "pcClose": ("Close", "Schließen"),
    "pcNotHere": ("The patent check works on the pages of Impress and Draw documents.",
                  "Die Patentprüfung arbeitet auf den Seiten von Impress- und Draw-Dokumenten."),
    "pcNoUnits": ("No page has InventaSketch elements.", "Keine Seite enthält InventaSketch-Elemente."),
    "pcNone": ("No findings on %d page(s).", "Keine Befunde auf %d Seite(n)."),
    "pcSummary": ("%d finding(s), %d error(s) on %d page(s). Choose a finding to go to it.",
                  "%d Befund(e), %d Fehler auf %d Seite(n). Einen Befund wählen springt dorthin."),
    "pcSlide": ("Slide %d", "Folie %d"),
    "pcPage": ("Page %d", "Seite %d"),
    "pcSource": ("Source: %s", "Fundstelle: %s"),
    "pcError": ("Error", "Fehler"),
    "pcWarning": ("Warning", "Warnung"),
    "pcManual": ("Check by hand", "Selbst prüfen"),
    "pcInfo": ("Note", "Hinweis"),
}

_STATE = {"lang": "en"}


def detect(locale):
    """'de' if the UI locale starts with 'de', otherwise 'en'."""
    return "de" if (locale or "").lower().startswith("de") else "en"


def set_language(locale):
    _STATE["lang"] = detect(locale)
    return _STATE["lang"]


def language():
    return _STATE["lang"]


def text(key, lang=None):
    en, de = TEXTS[key]
    return de if (lang or _STATE["lang"]) == "de" else en
