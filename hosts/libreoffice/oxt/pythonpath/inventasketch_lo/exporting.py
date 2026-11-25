"""Export (rules without UNO, unit-tested): the formats offered for the selection or the page, default file names and
the bytes written.

The formats are the editable SVG (engine ``renderPageSvg`` with mode "export": named groups per element and part, texts
stay text) followed by the engine's domain exports that apply to the same elements (engine ``exporters``: LaTeX TikZ,
chart data for matplotlib and pgfplots, bill of materials, netlists …; run with ``export``). Like the Office web
add-in, an export covers the selected elements if there are any, else the current slide/page.
"""

import os

from . import i18n

SVG = "svg.editable"


def formats(exporters, lang=None):
    """[{id, name, extension, preserves}]: the editable SVG first, then the engine exporters in their order (an
    exporter without id or extension is left out)."""
    out = [{"id": SVG, "name": i18n.text("exSvg", lang), "extension": "svg",
            "preserves": i18n.text("exSvgPreserves", lang)}]
    seen = {SVG}
    for e in exporters or []:
        fid, ext = e.get("id"), e.get("extension")
        if not fid or not ext or fid in seen:
            continue
        seen.add(fid)
        out.append({"id": fid, "name": e.get("name") or fid, "extension": ext.lstrip("."),
                    "preserves": e.get("preserves") or ""})
    return out


def find(fmts, fid):
    """The format with id ``fid`` (also accepted: its extension, e.g. "svg" or "tex"), or None."""
    for f in fmts:
        if f["id"] == fid:
            return f
    for f in fmts:
        if f["extension"] == (fid or "").lstrip(".").lower():
            return f
    return None


def row(fmt):
    """List row of a format: "LaTeX (TikZ) (.tex)"."""
    return "%s (.%s)" % (fmt["name"], fmt["extension"])


def scope_text(count, kind, lang=None):
    """What the export covers: the selected elements, else the current slide, page or sheet."""
    if count:
        return i18n.text("exSelection", lang) % count
    return i18n.text({"impress": "exSlide", "draw": "exPage"}.get(kind, "exSheet"), lang)


def default_name(fmt, selection):
    """Default file name, as in the web add-in: "inventasketch.svg" or "inventasketch-selection.tex"."""
    return "inventasketch%s.%s" % ("-selection" if selection else "", fmt["extension"])


def ensure_extension(path, extension):
    """``path`` with the format's extension appended when it has none or another one."""
    ext = "." + extension.lstrip(".")
    return path if os.path.splitext(path)[1].lower() == ext.lower() else path + ext


def file_bytes(text, extension):
    """UTF-8 bytes of an export; CSV gets a byte order mark so spreadsheet programs read umlauts correctly."""
    data = (text or "").encode("utf-8")
    return (b"\xef\xbb\xbf" + data) if extension.lstrip(".").lower() == "csv" else data


def filter_pattern(fmt):
    """(title, pattern) of the file picker filter of a format."""
    return "%s (*.%s)" % (fmt["name"], fmt["extension"]), "*." + fmt["extension"]
