"""Pure rules of the selection panel and the page session, without UNO (unit-testable).

The domain decisions come from the engine (search ranking, fitting parts, which transforms change something, the
selection-wide operations, F, ports, motion); this module only turns the engine's answers into what the sidebar shows
and decides which units on the page have to be redrawn after an engine operation:

- recent library items per user (settings file next to the log), most recent first;
- the context-only action list of the selection panel in the fixed order of the UX rules (roadmap, plan 2026-11-24);
- labels of build ports and search rows, the typed turn angle;
- page session bookkeeping: which units to import again so every unit's own objects come from its own payload, which
  objects to redraw after an operation and which units became stale or empty.
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
