"""Library catalog for the sidebar, without UNO (unit-testable): filtering by category and search text, thumbnail
files. The catalog itself (names in the UI language, keywords, SVG thumbnails) comes from the engine."""

import os
import re
import unicodedata

from .log import log_dir


def _fold(text):
    text = unicodedata.normalize("NFKD", text or "").lower()
    return "".join(c for c in text if not unicodedata.combining(c)).replace("ß", "ss")


def filter_items(catalog, category=None, query=""):
    """Items of a category (None = all) whose name or keywords contain every word of the query, in catalog order of
    the categories, then by name."""
    words = [w for w in _fold(query).split() if w]
    order = {c["id"]: i for i, c in enumerate(catalog.get("categories") or [])}
    out = []
    for item in catalog.get("items") or []:
        if category and item.get("category") != category:
            continue
        if words:
            hay = _fold(" ".join([item.get("name", ""), item.get("id", "")] + list(item.get("keywords") or [])))
            if not all(w in hay for w in words):
                continue
        out.append(item)
    out.sort(key=lambda i: (order.get(i.get("category"), 1 << 20), _fold(i.get("name", ""))))
    return out


def category_entries(catalog):
    """[(id, name)] of the categories that have items."""
    used = {i.get("category") for i in catalog.get("items") or []}
    return [(c["id"], c["name"]) for c in catalog.get("categories") or [] if c["id"] in used]


_SAFE = re.compile(r"[^0-9A-Za-z_.-]")


def thumbnail_path(item, lang, base=None):
    """Writes the item's SVG thumbnail to the cache (once) and returns the file path (None without thumbnail)."""
    svg = item.get("thumbnail")
    if not svg:
        return None
    folder = os.path.join(base or log_dir(), "thumbnails", lang)
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, _SAFE.sub("_", item["id"]) + ".svg")
    data = svg.encode("utf-8")
    if not os.path.isfile(path) or os.path.getsize(path) != len(data):
        with open(path, "wb") as f:
            f.write(data)
    return path
