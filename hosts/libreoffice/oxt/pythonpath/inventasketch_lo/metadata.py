"""Where InventaSketch keeps the model of a unit in LibreOffice documents (ADR-0006), without UNO (unit-testable).

What survives which format (verified with LibreOffice 26.8 by tests/integration_test.py, see docs/en/hosts/libreoffice.md):

- Shape name ``ISK:<type>:<id>`` on the unit (group or single shape): .odp and .pptx (cNvPr name).
- UserDefinedAttributes on the unit (namespace ``urn:inventasketch:shape:1``; groups have none in LibreOffice, so for
  groups they sit on the first leaf shape): id, type, compact model (``isk1:``) and the host bounds and rotation after
  drawing. Written to .odp/.odg/.ods as ODF attributes; lost in .pptx. They travel with copy/paste inside LibreOffice.
- User-defined document properties ``ISK_MODEL_<id>`` (chunked: ``_1``, ``_2`` ...) and ``ISK_BOUNDS_<id>``: the
  document-level mirror; survives .odp and .pptx (docProps/custom.xml).

Reading prefers the attributes on the shape and falls back to the document properties.
"""

import json
import re

NAME_PREFIX = "ISK:"
ATTR_NAMESPACE = "urn:inventasketch:shape:1"
ATTR_ID = "isk:id"
ATTR_TYPE = "isk:type"
ATTR_MODEL = "isk:model"
ATTR_BOUNDS = "isk:bounds"
MODEL_PREFIX = "ISK_MODEL_"
BOUNDS_PREFIX = "ISK_BOUNDS_"
# Maximum length of one document property value; longer models continue in ISK_MODEL_<id>_1, _2 ...
CHUNK = 60000

_ID = re.compile(r"^[0-9A-Za-z_.-]{1,64}$")


def unit_name(unit_type, unit_id):
    return "%s%s:%s" % (NAME_PREFIX, unit_type, unit_id)


def parse_name(name):
    """(type, id) from a unit name ``ISK:<type>:<id>``; None for other shapes."""
    if not name or not name.startswith(NAME_PREFIX):
        return None
    rest = name[len(NAME_PREFIX):]
    if ":" not in rest:
        return None
    unit_type, unit_id = rest.rsplit(":", 1)
    if not unit_type or not _ID.match(unit_id):
        return None
    return unit_type, unit_id


def model_property(unit_id):
    return MODEL_PREFIX + unit_id


def bounds_property(unit_id):
    return BOUNDS_PREFIX + unit_id


def split_value(value, chunk=CHUNK):
    """Value in parts of at most ``chunk`` characters (at least one part)."""
    value = value or ""
    return [value[i:i + chunk] for i in range(0, len(value), chunk)] or [""]


def chunk_names(base, count):
    return [base] + ["%s_%d" % (base, i) for i in range(1, count)]


def join_chunks(read, base):
    """Joins ``base``, ``base_1``, ``base_2`` ... read with ``read(name)`` (None = missing); None if ``base`` is missing."""
    first = read(base)
    if first is None:
        return None
    parts, i = [first], 1
    while True:
        part = read("%s_%d" % (base, i))
        if part is None:
            break
        parts.append(part)
        i += 1
    return "".join(parts)


def bounds_json(rect):
    """Host rectangle (pt) and RotateAngle ("r", 1/100 degree counter-clockwise) as compact JSON."""
    data = {k: round(float(rect[k]), 3) for k in ("x", "y", "w", "h")}
    data["r"] = int(rect.get("r") or 0)
    return json.dumps(data, separators=(",", ":"))


def parse_bounds(text):
    try:
        b = json.loads(text) if text else None
        if not isinstance(b, dict):
            return None
        out = {k: float(b[k]) for k in ("x", "y", "w", "h")}
        out["r"] = int(b.get("r") or 0)
        return out
    except (ValueError, KeyError, TypeError):
        return None


def rotation_delta(stored, current):
    """Native rotation since drawing in degrees clockwise (engine convention), in (-180, 180]."""
    delta = ((int(current.get("r") or 0) - int(stored.get("r") or 0)) / 100.0) % 360.0
    if delta > 180:
        delta -= 360
    return -delta if delta else 0.0


def transform_needed(stored, current, tolerance=0.05):
    """True if the unit was moved, resized or rotated natively since it was drawn (pt, tolerance in pt)."""
    if not stored or not current:
        return False
    return abs(rotation_delta(stored, current)) > 1e-6 or \
        any(abs(stored[k] - current[k]) > tolerance for k in ("x", "y", "w", "h"))


def host_transform(stored, current):
    """Request members for the engine's hostTransform: from/target rectangles (pt) and rotation (degrees clockwise).
    A rotated unit reports its rotated bounding box, so with a rotation only the move of the centre is taken over
    (rotating about the centre); a resize together with a rotation is not detectable."""
    rotation = rotation_delta(stored, current)
    src = {k: stored[k] for k in ("x", "y", "w", "h")}
    if abs(rotation) < 1e-6:
        return {"from": src, "target": {k: current[k] for k in ("x", "y", "w", "h")}}
    cx, cy = current["x"] + current["w"] / 2.0, current["y"] + current["h"] / 2.0
    target = {"x": cx - src["w"] / 2.0, "y": cy - src["h"] / 2.0, "w": src["w"], "h": src["h"]}
    return {"from": src, "target": target, "rotation": rotation}


def orphaned_properties(names, unit_ids):
    """Document properties of units that are no longer on any page."""
    alive = set(unit_ids)
    out = []
    for n in names:
        for prefix in (MODEL_PREFIX, BOUNDS_PREFIX):
            if n.startswith(prefix):
                rest = n[len(prefix):]
                base = re.sub(r"_\d+$", "", rest) if rest not in alive else rest
                if rest not in alive and base not in alive:
                    out.append(n)
    return out
