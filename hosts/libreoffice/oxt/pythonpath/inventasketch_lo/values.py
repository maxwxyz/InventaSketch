"""Pure rules of the Values and Calculator panels (docs/en/design/engineering-calculations.md sections 10–11), no UNO.

The engine computes everything (``values``, ``setValue``, ``clearValue``, ``explain``, ``checks``, ``calc``,
``convertUnit``, ``importCode``); this module turns its answers into panel rows, as the COM value card and the web
value card do:

- one row per value: marker (✎ entered, = derived, ○ default, ⇄ linked, ⚠ in conflict), symbol, name, the field text
  (the entered text as typed, else the formatted value), the unit, × for an entered value; under a derived value its
  formula step by step (Explore and Engineer; Engineer adds the source and a fourth significant digit);
- a status line (✓, what is missing, conflict) and every conflict with both values;
- the value level (Quick sketch, Explore, Engineer) is a document setting: in LibreOffice the user-defined document
  property ``ISK_VALUES`` (like the presentation tag of the COM add-in); Quick sketch is stored as "not set".
"""

from . import i18n

LEVELS = ("quick", "explore", "engineer")
LEVEL_PROPERTY = "ISK_VALUES"
_MARKERS = {"entered": "✎", "derived": "=", "default": "○", "linked": "⇄"}


def parse_level(text):
    """'quick', 'explore' or 'engineer' from a stored or typed level (anything else: quick)."""
    t = (text or "").strip().lower()
    return t if t in LEVELS else "quick"


def level_labels(lang=None):
    return [i18n.text("valLevel_" + level, lang) for level in LEVELS]


def digits(level):
    """Significant digits: three, four in Engineer."""
    return 4 if level == "engineer" else 3


def marker(v):
    if v.get("conflict"):
        return "⚠"
    return _MARKERS.get(v.get("provenance"), "")


def provenance_text(v, lang=None):
    key = "conflict" if v.get("conflict") else (v.get("provenance") or "unknown")
    return i18n.text("valProv_" + key if ("valProv_" + key) in i18n.TEXTS else "valProv_unknown", lang)


def visible_vars(vars_, level):
    """The card values, plus the panel and engineer ones in Engineer."""
    return list(vars_ or []) if level == "engineer" else [v for v in vars_ or [] if v.get("relevance", "card") == "card"]


def field_text(v):
    """What a value field starts with: the entered text as typed, else the formatted value."""
    return v.get("text") if v.get("provenance") == "entered" and v.get("text") else (v.get("display") or "")


def formula_line(v, level, explained=None):
    """The formula line under a derived or linked value (Explore and Engineer; Engineer adds the source), else ""."""
    if level == "quick" or v.get("provenance") not in ("derived", "linked"):
        return ""
    line = explained or v.get("formula") or ""
    if level == "engineer" and v.get("source") and line:
        line += " [%s]" % v["source"]
    return line


def explained_line(steps, key):
    """The explanation line of ``key`` from an ``explain`` answer (its own step, else the last)."""
    own = [s for s in steps or [] if s.get("key") == key]
    pick = own[-1] if own else (steps[-1] if steps else None)
    return (pick or {}).get("text") or ""


def status_line(obj, lang=None):
    """'✓ all values agree', the hint of what is missing, or the conflict."""
    if not obj:
        return ""
    if obj.get("status") == "checked":
        return "✓ " + i18n.text("valChecked", lang)
    return obj.get("hint") or ""


def rows(obj, level, explained=None, errors=None, lang=None):
    """Panel rows of one element's values: [{key, head ("✎ U Voltage"), field, unit, clear (bool), readonly (bool),
    detail, tip}]; ``readonly`` marks a text-valued variable (a class such as the Grashof class) that is never typed.
    ``explained``: key -> explanation line; ``errors``: key -> message of a refused entry (shown under the row)."""
    explained = explained or {}
    errors = errors or {}
    out = []
    for v in visible_vars((obj or {}).get("vars"), level):
        mark = marker(v)
        head = " ".join(x for x in (mark, v.get("symbol") or "", v.get("name") or v.get("key")) if x)
        detail = errors.get(v["key"]) or v.get("error") or formula_line(v, level, explained.get(v["key"]))
        out.append({"key": v["key"], "head": head, "field": field_text(v), "unit": v.get("unit") or "",
                    "clear": v.get("provenance") == "entered", "readonly": bool(v.get("readOnly")), "detail": detail,
                    "tip": "%s – %s" % (v.get("name") or v["key"], provenance_text(v, lang))})
    return out


def conflict_lines(obj):
    return ["⚠ " + (c.get("message") or "") for c in (obj or {}).get("conflicts") or []]


def needs_explain(v, level):
    return level != "quick" and v.get("provenance") in ("derived", "linked")


def shows_values_switch(params):
    """The element's ``showValues`` parameter (True/False), or None when its type has no such switch."""
    for p in params or []:
        if p.get("key") == "showValues":
            value = p.get("value")
            if value is None:
                value = p.get("default")
            return bool(value)
    return None


# ------------------------------------------------------------------ calculator and code import


def calc_lines(result, converted=None):
    """('= 3.68 kW', steps) of a ``calc`` answer, the result converted when a target unit was given."""
    shown = (converted or {}).get("text") or (result or {}).get("text") or ""
    return ("= " + shown if shown else ""), list((result or {}).get("steps") or [])[:6]


def code_report_lines(r, max_skipped=12):
    """Lines of an ``importCode`` report: the message, what was imported, then at most ``max_skipped`` skipped parts."""
    lines = [r.get("message") or ""] + ["✓ " + x for x in r.get("imported") or []]
    skipped = r.get("skipped") or []
    for s in skipped[:max_skipped]:
        code = s.get("code") or ""
        if len(code) > 40:
            code = code[:39] + "…"
        line = s.get("line") or 0
        lines.append("– %s%s%s" % ("%d: " % line if line > 0 else "", s.get("reason") or "",
                                   " (%s)" % code if code else ""))
    if len(skipped) > max_skipped:
        lines.append("… +%d" % (len(skipped) - max_skipped))
    return lines


CODE_EXTENSIONS = (".py", ".tex", ".tikz", ".pgf", ".txt")


def decode_code(raw):
    """Text of a code file (UTF-8 with or without BOM, else Windows-1252)."""
    try:
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError:
        return raw.decode("cp1252", errors="replace")
