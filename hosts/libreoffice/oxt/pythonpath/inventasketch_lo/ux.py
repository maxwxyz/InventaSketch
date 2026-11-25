"""Pure rules of the selection panel and the page session, without UNO (unit-testable).

The domain decisions come from the engine (search ranking, fitting parts, which transforms change something, the
selection-wide operations, F, ports, motion); this module only turns the engine's answers into what the sidebar shows
and decides which units on the page have to be redrawn after an engine operation:

- recent library items per user (settings file next to the log), most recent first;
- the context-only action list of the selection panel in the fixed order of the UX rules (roadmap, plan 2026-11-24);
- labels of build ports and search rows, the typed turn angle;
- page session bookkeeping: which units to import again so every unit's own objects come from its own payload, which
  objects to redraw after an operation and which units became stale or empty;
- on-canvas interaction (canvas.py): whether a mouse release ends a drag to snap, the common move of the dragged units,
  the snap tolerance at the zoom of the drag, the Ctrl+Space key and the context menu entries.
"""

import json
import os
import re

from . import i18n

RECENT_MAX = 12
SETTINGS_FILE = "libreoffice-settings.json"


# ------------------------------------------------------------------ recent items


def settings_path(base=None):
    from .log import log_dir
    return os.path.join(base or log_dir(), SETTINGS_FILE)


def load_recent(base=None):
    """Recently inserted library item ids, most recent first ([] without settings)."""
    try:
        with open(settings_path(base), encoding="utf-8") as f:
            data = json.load(f)
        items = data.get("recent") if isinstance(data, dict) else None
        return [str(i) for i in items if isinstance(i, str) and i][:RECENT_MAX] if isinstance(items, list) else []
    except (OSError, ValueError):
        return []


def remember(recent, item_id, limit=RECENT_MAX):
    """The recent list with ``item_id`` moved to the front (same rule as LibrarySearch.Remember in the engine)."""
    return ([item_id] + [i for i in (recent or []) if i != item_id])[:limit]


def save_recent(item_id, base=None):
    """Puts ``item_id`` at the front of the stored recent list; returns the new list. Never raises."""
    recent = remember(load_recent(base), item_id)
    path = settings_path(base)
    data = {}
    try:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError):
        pass
    if not isinstance(data, dict):
        data = {}
    try:
        data["recent"] = recent
        tmp = path + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=1)
        os.replace(tmp, path)
    except (OSError, ValueError):
        pass
    return recent


# ------------------------------------------------------------------ texts


def search_row(hit, lang=None):
    """List row of a search hit: the name, marked when it fits the selection."""
    name = hit.get("name") or hit.get("id") or ""
    return i18n.text("fitsRow", lang) % name if hit.get("fits") else name


_ANGLE = re.compile(r"^\s*([+-]?\d+(?:[.,]\d*)?|[+-]?[.,]\d+)\s*(?:°|deg|grad)?\s*$", re.IGNORECASE)


def parse_angle(text):
    """Typed turn angle in degrees ("90", "-45", "12,5", "30°"); None if empty, invalid or zero."""
    m = _ANGLE.match(text or "")
    if not m:
        return None
    value = float(m.group(1).replace(",", "."))
    return value if abs(value) > 1e-9 else None


_DIRECTIONS = [(0, "dirRight"), (45, "dirDownRight"), (90, "dirDown"), (135, "dirDownLeft"), (180, "dirLeft"),
               (225, "dirUpLeft"), (270, "dirUp"), (315, "dirUpRight")]


def direction_key(dx, dy):
    """Nearest of eight screen directions (y down) of an outward port direction; None for a zero vector."""
    import math
    if abs(dx) < 1e-9 and abs(dy) < 1e-9:
        return None
    angle = math.degrees(math.atan2(dy, dx)) % 360.0
    return min(_DIRECTIONS, key=lambda d: min(abs(angle - d[0]), 360 - abs(angle - d[0])))[1]


def port_label(port, index, lang=None):
    """'2: Mesh, right' for a build port of the engine ({id, kind, dx, dy})."""
    kind = port.get("kind") or ""
    key = "port" + kind
    name = i18n.text(key, lang) if key in i18n.TEXTS else kind
    direction = direction_key(float(port.get("dx") or 0), float(port.get("dy") or 0))
    if direction and kind not in ("Hub", "Bevel"):
        return "%d: %s, %s" % (index + 1, name, i18n.text(direction, lang))
    return "%d: %s" % (index + 1, name)


def info_line(info, lang=None):
    """Second line of the selection header: F = n for a link of a self-built linkage."""
    dof = info.get("dof") or {}
    if dof.get("dof") is not None:
        return i18n.text("dofInfo", lang) % (dof["dof"], dof.get("links") or 0, dof.get("joints") or 0)
    return ""


# ------------------------------------------------------------------ selection panel


def selection_actions(info):
    """Context-only buttons of the selection panel as [(command, argument, label key or text, tooltip key or text)],
    in the fixed order of the UX rules: element actions (path, ghost positions) | assembly, explode | transforms.
    Multi-selection: the engine's selection-wide operations (labels from the engine), then "select assembly".
    Build "+" and turning have their own rows (``show_build``, ``show_turn``)."""
    if not info:
        return []
    out = []
    if info.get("count", 1) >= 2:
        for opt in info.get("arrange") or []:
            out.append(("arrange", opt["id"], opt.get("label") or opt["id"], opt.get("label") or opt["id"]))
        if info.get("assemblyExtra"):
            out.append(("assembly", None, "actAssembly", "tipAssembly"))
        return out
    dof = info.get("dof") or {}
    if dof.get("dof") == 1:
        out.append(("drawTrace", None, "actTrace", "tipTrace"))
        out.append(("drawGhosts", None, "actGhosts", "tipGhosts"))
    if info.get("assemblyExtra"):
        out.append(("assembly", None, "actAssembly", "tipAssembly"))
    if dof.get("explodable"):
        out.append(("explode", None, "actExplode", "tipExplode"))
    effect = info.get("effect") or {}
    if effect.get("mirrorH"):
        out.append(("transform", "mirrorH", "actMirrorH", "tipMirrorH"))
    if effect.get("mirrorV"):
        out.append(("transform", "mirrorV", "actMirrorV", "tipMirrorV"))
    if effect.get("rotate90"):
        out.append(("transform", "rotate90", "actRotate", "tipRotate"))
    return out


def panel_actions(info):
    """The buttons of the selection panel: "Text …" (elements with a main text), the element's own engine actions that
    are no option of a choice (``choices.element_choices``; labels from the engine), then ``selection_actions``.
    "Select assembly" gives way to the "Select connected" list when the engine offers connected kinds (it is the
    kind "moves along"). Entries as in ``selection_actions``; engine actions are ("action", action dict, label, tip)."""
    if not info:
        return []
    from . import choices
    out = []
    if info.get("count", 1) == 1 and info.get("mainText"):
        out.append(("text", None, "actText", "tipText"))
    _, plain = choices.element_choices(info)
    for a in plain:
        label = choices.option_label(a)
        tip = label + (" – " + a["input"].get("label") if a.get("input") and a["input"].get("label") else "")
        out.append(("action", a, label, tip))
    for entry in selection_actions(info):
        if entry[0] == "assembly" and info.get("connect"):
            continue
        out.append(entry)
    return out


def label_of(key_or_text, lang=None):
    """i18n text for a key, otherwise the text itself (engine labels are already localised)."""
    return i18n.text(key_or_text, lang) if key_or_text in i18n.TEXTS else key_or_text


def show_views(info):
    return bool(info) and info.get("count", 1) == 1 and len(info.get("views") or []) > 1


def show_build(info):
    return bool(info) and info.get("count", 1) == 1 and bool(info.get("ports"))


def show_turn(info):
    """Turning by an angle only for a gear that is coupled with others (a lone gear only spins in place)."""
    motion = (info or {}).get("motion") or {}
    return bool(info) and info.get("count", 1) == 1 and motion.get("kind") == "gear" and bool(motion.get("coupled"))


def assembly_extra(assembly_ids, selected_ids):
    """Number of coupled objects that are not selected yet ("select assembly" only does something then)."""
    selected = set(selected_ids)
    return len([i for i in assembly_ids or [] if i not in selected])


# ------------------------------------------------------------------ page session bookkeeping


def own_ids(unit_id, imported, groups):
    """Object ids that belong to a unit: the object with the unit id or the members of the group with that id; the
    rest of a payload are copies of attachment targets. ``groups``: object id -> group id. Falls back to everything
    imported when nothing matches (old payloads)."""
    own = [i for i in imported if i == unit_id or groups.get(i) == unit_id]
    return own or list(imported)


def reimports(order, own, contents, rounds=3):
    """Units to import again, in order, so that each unit's own objects end up from its own payload (a payload also
    carries copies of the objects its points are attached to, which may be older than the owner's model). Simulates
    the last writer of every object after importing ``order``; mutual attachments are given up after ``rounds``."""
    last = {}
    for u in order:
        for o in contents.get(u, ()):
            last[o] = u
    seq = []
    for _ in range(rounds):
        bad = [u for u in order if any(last.get(o) != u for o in own.get(u, ()))]
        if not bad:
            break
        for u in bad:
            seq.append(u)
            for o in contents.get(u, ()):
                last[o] = u
    return seq


def redraw_targets(units, known, existing, changed, dependents, force=()):
    """What to redraw after an engine operation in a page session.

    units: unit id -> own object ids (units on the page); known: every object id imported before the operation;
    existing: object ids after the operation; changed/dependents: from the editor state; force: unit ids redrawn anyway
    (selected units moved natively, "redraw"). Returns (object ids for drawPlan, unit ids whose objects are all gone).
    Objects that are new (not imported before) are drawn as new units; copies of attachment targets whose own unit is
    not on the page are never drawn."""
    touched = set(changed or ()) | set(dependents or ())
    force = set(force or ())
    existing = set(existing)
    out = []
    seen = set()

    def add(i):
        if i in existing and i not in seen:
            seen.add(i)
            out.append(i)
    owned = set()
    for uid, objs in units.items():
        owned.update(objs)
        if uid in force or any(o in touched for o in objs):
            for o in objs:
                add(o)
    for i in changed or ():
        if i not in known and i not in owned:
            add(i)
    gone = [uid for uid, objs in units.items() if objs and not any(o in existing for o in objs)]
    return out, gone


def stale_units(units, plan_units):
    """Units on the page whose objects are now drawn by a plan unit with another id (grouping, merging)."""
    fresh = {u["id"] for u in plan_units}
    covered = set()
    for u in plan_units:
        covered.update(u.get("objectIds") or ())
    return [uid for uid, objs in units.items() if uid not in fresh and any(o in covered for o in objs)]


def units_containing(units, ids):
    """Unit ids (page order of ``units``) that contain one of the object ids."""
    wanted = set(ids or ())
    return [uid for uid, objs in units.items() if any(o in wanted for o in objs)]


# ------------------------------------------------------------------ on-canvas interaction (canvas.py)

# Snap distance on screen, as in the PowerPoint COM add-in and the Office web add-in (8 px).
SNAP_PX = 8.0
# UNO key code and modifiers (com.sun.star.awt.Key, KeyModifier, MouseButton).
KEY_SPACE = 1284
KEY_RETURN = 1280
KEY_ESCAPE = 1281
MOD_SHIFT, MOD_CTRL, MOD_ALT = 1, 2, 4
MOUSE_LEFT = 1


def drag_delta(pairs, tolerance=0.05):
    """The common translation (dx, dy in pt) of the selected units after a mouse drag, or None.

    ``pairs``: [(stored bounds, current bounds)] per selected unit (pt, see metadata.bounds_json). Only a pure move
    counts: no unit resized or rotated, every unit moved by the same vector, and the move longer than ``tolerance``."""
    from . import metadata
    delta = None
    for stored, current in pairs or ():
        if not stored or not current:
            return None
        if abs(metadata.rotation_delta(stored, current)) > 1e-6:
            return None
        if abs(stored["w"] - current["w"]) > tolerance or abs(stored["h"] - current["h"]) > tolerance:
            return None
        d = (current["x"] - stored["x"], current["y"] - stored["y"])
        if delta is None:
            delta = d
        elif abs(d[0] - delta[0]) > tolerance or abs(d[1] - delta[1]) > tolerance:
            return None
    if delta is None or (delta[0] ** 2 + delta[1] ** 2) ** 0.5 <= tolerance:
        return None
    return delta


def snap_tolerance(delta_mm, drag_px, snap_px=SNAP_PX, low=0.5, high=10.0):
    """Snap tolerance in mm for a drag: ``snap_px`` screen pixels at the zoom of the drag (mm moved per pixel the mouse
    moved), kept within [low, high]; None (engine default) without a usable pixel distance."""
    try:
        px = float(drag_px)
    except (TypeError, ValueError):
        return None
    mm = (delta_mm[0] ** 2 + delta_mm[1] ** 2) ** 0.5
    if px < 4 or mm <= 0:
        return None
    return max(low, min(high, snap_px * mm / px))


def is_quick_insert_key(key_code, modifiers):
    """Ctrl+Space (Cmd+Space on macOS, MOD1 in UNO) without Shift or Alt."""
    return key_code == KEY_SPACE and (modifiers or 0) & (MOD_SHIFT | MOD_CTRL | MOD_ALT) == MOD_CTRL


def snaps_after_release(buttons, modifiers, press, release):
    """True if a mouse release ends a drag that may move units and should snap: left button, no Ctrl (copy) or Alt
    (free move, as in the other hosts), and the mouse moved at least two pixels between press and release."""
    if press is None or release is None or not (buttons or 0) & MOUSE_LEFT:
        return False
    if (modifiers or 0) & (MOD_CTRL | MOD_ALT):
        return False
    return ((release[0] - press[0]) ** 2 + (release[1] - press[1]) ** 2) ** 0.5 >= 2


def command_url(command, **args):
    """``inventasketch.libreoffice:<command>?k=v`` with URL-encoded values (parse_command decodes them)."""
    from urllib.parse import quote
    query = "&".join("%s=%s" % (k, quote(str(v), safe="")) for k, v in sorted(args.items()) if v is not None)
    return "inventasketch.libreoffice:%s%s" % (command, "?" + query if query else "")


CHECKED_ARG = "on=1"


def checked_url(url):
    """The command URL of a checked menu entry (the current option of a choice): the protocol handler reports the
    status ``State`` = True for it, so LibreOffice draws the check mark itself."""
    return url + ("&" if "?" in url else "?") + CHECKED_ARG


def is_checked_url(url):
    query = (url or "").partition("?")[2]
    return CHECKED_ARG in query.split("&")


def is_disabled_url(url):
    """A greyed place of the context menu (command ``place``): its status is disabled."""
    return (url or "").startswith("inventasketch.libreoffice:place")


def is_select_connected_click(buttons, click_count, modifiers):
    """Ctrl+double-click with the left button (select connected, the most meaningful kind)."""
    return bool((buttons or 0) & MOUSE_LEFT) and (click_count or 0) == 2 and \
        (modifiers or 0) & (MOD_SHIFT | MOD_CTRL | MOD_ALT) == MOD_CTRL


def context_entries(info, lang=None):
    """Context menu entries for the selected unit(s): the eight fixed places (``choices.context_entries``)."""
    from . import choices
    return choices.context_entries(info, lang)


# ------------------------------------------------------------------ compatibility check

_SEVERITY_ORDER = {"error": 0, "warning": 1, "manual": 2, "info": 3}
_SEVERITY_KEY = {"error": "pcError", "warning": "pcWarning", "manual": "pcManual", "info": "pcInfo"}
_URL = re.compile(r"\s*<https?://[^>\s]+>")


def order_findings(findings):
    """Errors first, then warnings, points to check by hand and notes; page and engine order within each."""
    indexed = list(enumerate(findings or []))
    indexed.sort(key=lambda p: (_SEVERITY_ORDER.get(p[1].get("severity"), 9), p[1].get("page") or 0, p[0]))
    return [f for _, f in indexed]


def severity_label(severity, lang=None):
    key = _SEVERITY_KEY.get(severity)
    return i18n.text(key, lang) if key else (severity or "")


def finding_row(f, slides=True, lang=None):
    """List row: 'Slide 2 · Error · message' ('Page' in Draw)."""
    where = i18n.text("pcSlide" if slides else "pcPage", lang) % (f.get("page") or 0)
    return "%s · %s · %s" % (where, severity_label(f.get("severity"), lang), f.get("message") or "")


def part_row(p, lang=None):
    """Parts list row: 'Series 1' or 'Major grid (hidden)'."""
    return (p.get("label") or p.get("key") or "") + (i18n.text("ptHidden", lang) if p.get("hidden") else "")


def part_action_row(a, lang=None):
    """Actions list row: the label, '…' when the action needs a value, '✓' when it is active."""
    label = a.get("label") or a.get("id") or ""
    if a.get("input") and not label.endswith("…"):
        label += " …"
    return ("✓ " if a.get("active") else "") + label


def source_text(source):
    """The cited sources without their links: 'DE-01: PatV § 6(2) <https://…>; …' → 'DE-01: PatV § 6(2); …'."""
    return _URL.sub("", source or "").strip()


def finding_detail(f, lang=None):
    """Detail lines of a chosen finding: suggestion and sources."""
    lines = []
    if f.get("suggestion"):
        lines.append(f["suggestion"])
    if f.get("source"):
        lines.append(i18n.text("pcSource", lang) % source_text(f["source"]))
    return "\n".join(lines)


def findings_summary(findings, pages, lang=None):
    """'3 finding(s), 1 error(s) on 2 page(s)'; a hint when no page has InventaSketch elements."""
    if not pages:
        return i18n.text("pcNoUnits", lang)
    if not findings:
        return i18n.text("pcNone", lang) % pages
    errors = sum(1 for f in findings if f.get("severity") == "error")
    return i18n.text("pcSummary", lang) % (len(findings), errors, pages)


# ------------------------------------------------------------------ variants (slide comparison)

DIFF_GREEN, DIFF_ORANGE, DIFF_GREY = 0x2E9A3C, 0xF58000, 0x8C8C8C


def diff_marks(diff, pad=1.5):
    """Frames of the engine's ``diff`` answer: [(kind, (x, y, w, h) in mm, RGB colour, dashed)], added green, changed
    orange, removed dashed grey; each frame ``pad`` mm larger than the element and at least 4 mm wide and high."""
    out = []
    for kind, colour, dashed in (("added", DIFF_GREEN, False), ("changed", DIFF_ORANGE, False),
                                 ("removed", DIFF_GREY, True)):
        for e in diff.get(kind) or []:
            w, h = max(4.0, e["w"] + 2 * pad), max(4.0, e["h"] + 2 * pad)
            cx, cy = e["x"] + e["w"] / 2.0, e["y"] + e["h"] / 2.0
            out.append((kind, (cx - w / 2.0, cy - h / 2.0, w, h), colour, dashed))
    return out


def diff_legend(diff, previous, slides=True, lang=None):
    """'Compared with slide 1: 1 added, 1 changed, 1 removed' (page in Draw)."""
    return i18n.text("cmpLegendSlide" if slides else "cmpLegendPage", lang) % (
        previous, len(diff.get("added") or []), len(diff.get("changed") or []), len(diff.get("removed") or []))

