"""User-facing texts of the extension, English and German (CLAUDE.md: German if the UI language starts with "de",
otherwise English). Library names come from the engine catalog in the same language."""

TEXTS = {
    "title": ("InventaSketch", "InventaSketch"),
    "search": ("Search", "Suchen"),
    "category": ("Category", "Kategorie"),
    "allCategories": ("All categories", "Alle Kategorien"),
    "insert": ("Insert", "Einfügen"),
    "items": ("%d elements", "%d Elemente"),
    "selected": ("Selected unit", "Ausgewählte Einheit"),
    "noUnit": ("No InventaSketch unit selected.", "Keine InventaSketch-Einheit ausgewählt."),
    "editSelected": ("Edit selected unit", "Ausgewählte Einheit bearbeiten"),
    "view": ("View", "Ansicht"),
    "redraw": ("Redraw from model", "Aus dem Modell neu zeichnen"),
    "loading": ("Loading library ...", "Bibliothek wird geladen ..."),
    "undoInsert": ("InventaSketch: insert %s", "InventaSketch: %s einfügen"),
    "undoEdit": ("InventaSketch: edit %s", "InventaSketch: %s bearbeiten"),
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
