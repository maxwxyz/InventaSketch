"""Pure rules of the drawing-wide settings in LibreOffice, no UNO (docs/en/design/interaction-ux.md section 6b,
docs/en/design/engineering-calculations.md section 10.11):

- **Colour by medium** (engine ``setColorBy``, ``mediumColours``, ``insertLegend``): a page-wide display mode "Off (own
  colours)" / "By medium". The mode lives in the models of the page's units (the engine adopts it from every imported
  unit), so a page shows the mode of its units; a new unit takes it (``payload_settings`` reads it from a unit's stored
  model without the engine).
- **Designations across pages** (engine ``designations``, ``designationsOf``, ``setContinuations``): the designations of
  the other pages' units become continuation markers "→ W3, slide 4" on the page drawn now; "Same name on slide 4"
  jumps there.
- **Check settings and unit preferences** (engine ``muteCheck``, ``setUnitPreference``): document settings, stored as
  the user-defined document properties ``ISK_MUTED`` (check ids separated by ";") and ``ISK_UNITS``
  ("pressure=MPa;energy=MJ"), the formats of the COM add-in's presentation tags; applied to every page session, like
  the value level ``ISK_VALUES``. The default unit of a quantity is stored as "not set".
- **Calculation sheet** (engine ``calcSheet``, ``insertCalcTable``): CSV, Markdown or LaTeX, copy, save, insert as table.
"""

import base64
import gzip
import json

from . import i18n

COLOR_MODES = ("off", "medium")
MUTED_PROPERTY = "ISK_MUTED"
UNITS_PROPERTY = "ISK_UNITS"
# The preferred units offered (quantity kind id, unit symbols; the first is the default), as in the COM add-in, but
# with the engine's "rev/s" for revolutions per second (the engine has no "s⁻¹" for a speed of rotation).
OFFERED_UNITS = (
    ("pressure", ("bar", "MPa", "kPa")),
    ("rotationalSpeed", ("min⁻¹", "rev/s", "rad/s")),
    ("energy", ("kWh", "MJ", "J")),
    ("power", ("W", "kW")),
)
SHEET_MODES = ("csv", "markdown", "latex")
SHEET_EXTENSIONS = {"csv": "csv", "markdown": "md", "latex": "tex"}


# ------------------------------------------------------------------ colour by medium


def color_labels(lang=None):
    return [i18n.text("colorBy_" + m, lang) for m in COLOR_MODES]


def parse_color_mode(text):
    """'medium' or 'off' from a typed or stored mode (the engine's empty/None is 'off')."""
    t = (text or "").strip().lower()
    if t in ("medium", "on", "1", "bymedium"):
        return "medium"
    if t in ("", "off", "none", "0"):
        return "off"
    raise ValueError(i18n.text("colorByUnknown") % text)


def payload_settings(payload):
    """The document settings stored in a compact model ("isk1:" + base64 of gzip JSON), {} when unreadable."""
    try:
        if not payload or not payload.startswith("isk1:"):
            return {}
        data = json.loads(gzip.decompress(base64.b64decode(payload[5:])).decode("utf-8"))
        return data.get("settings") or {}
    except (ValueError, OSError, EOFError, TypeError, AttributeError):
        return {}


def color_mode_of(payload):
    return "medium" if payload_settings(payload).get("colorBy") == "medium" else "off"


def changed_plan_units(before, after):
    """Ids of the plan units whose drawing differs between two draw plans of the same page (a drawing-wide setting
    such as a preferred unit changes only the units that show it); the stored models are not compared."""
    old = {u.get("id"): json.dumps(u.get("ops"), sort_keys=True) for u in (before or {}).get("units") or []}
    return [u.get("id") for u in (after or {}).get("units") or []
            if old.get(u.get("id")) != json.dumps(u.get("ops"), sort_keys=True)]


def legend_lines(colours):
    """Log lines of ``mediumColours`` mode "used": "L1 #8d5524 (IEC 60445)"."""
    return ["%s %s%s" % (c.get("name") or c.get("id"), c.get("color") or "",
                         " (%s)" % c["source"] if c.get("source") else "") for c in colours or []]


# ------------------------------------------------------------------ designations across pages


def page_word(kind):
    """How the engine names pages in continuation markers: "slide" in Impress, "page" in Draw."""
    return "slide" if kind == "impress" else "page"


def continuation_map(pages, current):
    """{designation key: [labels of the other pages that have it]} from ``pages`` = [(label, keys)] in page order;
    the page ``current`` (its label) is left out, keys without another page too."""
    out = {}
    for label, keys in pages:
        if label == current:
            continue
        for key in keys or ():
            labels = out.setdefault(key, [])
            if label not in labels:
                labels.append(label)
    return out


def elsewhere(designations, selected_ids, cmap):
    """[(key, designation as typed, [page labels])] of the selection's designations that also occur on other pages
    (``designations``: the engine's {key, designation, ids} of the page)."""
    chosen = set(selected_ids or ())
    out = []
    for d in designations or []:
        if chosen & set(d.get("ids") or ()) and cmap.get(d.get("key")):
            out.append((d["key"], d.get("designation") or d["key"], list(cmap[d["key"]])))
    return out


def same_name_label(designation, page_label, kind, lang=None):
    """"Same name on slide 4: W3" (context menu and the Selection panel's list)."""
    word = i18n.text("slide" if kind == "impress" else "page", lang)
    return i18n.text("sameNameOn", lang) % (word, page_label, designation)


def same_name_summary(found, kind, lang=None):
    """"Same name: also on slides 2, 4" for the log and the panel's tooltip (all of the selection's designations)."""
    labels = []
    for _, _, pages in found or []:
        labels += [p for p in pages if p not in labels]
    if not labels:
        return ""
    word = i18n.text("slides" if kind == "impress" else "pages", lang)
    return i18n.text("sameNameAlso", lang) % (word, ", ".join(labels))


# ------------------------------------------------------------------ check settings and unit preferences


def parse_muted(text):
    """Muted check ids from ``ISK_MUTED`` (";"-separated), in order, without duplicates."""
    out = []
    for part in (text or "").split(";"):
        p = part.strip()
        if p and p not in out:
            out.append(p)
    return out


def with_muted(text, check_id, mute):
    """``ISK_MUTED`` after muting (``mute`` True) or unmuting ``check_id``; "" when nothing is muted."""
    ids = [i for i in parse_muted(text) if i != check_id]
    if mute and check_id:
        ids.append(check_id)
    return ";".join(ids)


def parse_units(text):
    """{quantity kind: unit} from ``ISK_UNITS`` ("pressure=MPa;energy=MJ")."""
    out = {}
    for pair in (text or "").split(";"):
        kind, sep, unit = pair.partition("=")
        if sep and kind.strip() and unit.strip():
            out[kind.strip()] = unit.strip()
    return out


def format_units(units):
    return ";".join("%s=%s" % (k, units[k]) for k in sorted(units) if units[k])


def default_unit(kind):
    return next((units[0] for k, units in OFFERED_UNITS if k == kind), None)


def with_unit(text, kind, unit):
    """``ISK_UNITS`` after choosing ``unit`` for ``kind``: the default unit (or an empty one) clears the preference."""
    units = parse_units(text)
    if not unit or unit == default_unit(kind):
        units.pop(kind, None)
    else:
        units[kind] = unit
    return format_units(units)


def unit_rows(text, lang=None):
    """The "Show values in" dropdowns: [{kind, label, units, selected}] (the default selected when nothing is set)."""
    units = parse_units(text)
    rows = []
    for kind, offered in OFFERED_UNITS:
        current = units.get(kind) or offered[0]
        rows.append({"kind": kind, "label": i18n.text("qty_" + kind, lang), "units": list(offered),
                     "selected": offered.index(current) if current in offered else 0})
    return rows


def settings_calls(muted_text, units_text):
    """The engine calls that put the document settings into a page session: [(method, params)]."""
    calls = [("muteCheck", {"key": i}) for i in parse_muted(muted_text)]
    calls += [("setUnitPreference", {"quantityKind": k, "unit": u}) for k, u in sorted(parse_units(units_text).items())]
    return calls


def check_rows(checks, object_id, lang=None):
    """The plausibility warnings of one element for the Values panel, as in the COM value card: [{id, text, severity}]
    with "⚠ message (rule) [source]" (information: "ⓘ")."""
    rows = []
    for c in checks or []:
        if object_id is not None and c.get("objectId") not in (None, object_id):
            continue
        sev = c.get("severity") or "warning"
        text = ("⚠ " if sev in ("warning", "error") else "ⓘ ") + (c.get("message") or "")
        if c.get("rule"):
            text += " (%s)" % c["rule"]
        if c.get("source"):
            text += " [%s]" % c["source"]
        rows.append({"id": c.get("id") or "", "text": text, "severity": sev})
    return rows


# ------------------------------------------------------------------ calculation sheet


def sheet_labels(lang=None):
    return [i18n.text("sheet_" + m, lang) for m in SHEET_MODES]


def parse_sheet_mode(text):
    t = (text or "csv").strip().lower()
    t = {"md": "markdown", "tex": "latex"}.get(t, t)
    if t not in SHEET_MODES + ("table",):
        raise ValueError(i18n.text("sheetUnknown") % text)
    return t


def sheet_default_name(mode, selection):
    return "inventasketch-%s.%s" % ("calculation-selection" if selection else "calculation", SHEET_EXTENSIONS[mode])


def sheet_scope(count, kind, lang=None):
    """What the sheet covers: the selected elements, else the slide, page or sheet."""
    if count:
        return i18n.text("sheetSelection", lang) % count
    return i18n.text({"impress": "sheetSlide", "draw": "sheetPage"}.get(kind, "sheetSheet"), lang)


def sheet_summary(result, lang=None):
    """"12 values, 2 sources" under the text."""
    return i18n.text("sheetCounts", lang) % (result.get("rows") or 0, len(result.get("sources") or []))
