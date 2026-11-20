"""InventaSketch for LibreOffice (Impress, Draw, Calc): Python-UNO side of the extension.

Domain logic lives in the C# engine process (hosts/libreoffice/engine); this package only translates draw plans into
native UNO shapes, stores the model on them and provides the sidebar and menu (ADR-0002, ADR-0005).
VERSION is replaced by scripts/package.py with the version from Directory.Build.props.
"""

EXTENSION_ID = "org.inventasketch.libreoffice"
VERSION = "0.0.0-dev"
