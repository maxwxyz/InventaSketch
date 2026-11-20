"""Draw plan -> LibreOffice shape specifications, without UNO (unit-testable).

The engine (DrawPlanBuilder with DrawCapabilities.LibreOffice) delivers units of ops in points, y down, rotation in
degrees clockwise. This module translates each op into a plain dictionary that ``render.py`` turns into a native UNO
shape:

    {"kind": "rect" | "ellipse" | "line" | "polyline" | "polygon" | "bezier" | "text" | "group" | "svg",
     "service": "com.sun.star.drawing.RectangleShape", ...
     "x", "y", "w", "h": box in 1/100 mm (box shapes),
     "points": [[(x, y), ...], ...]            (line, polyline, polygon; 1/100 mm),
     "bezier": {"coords": [[(x, y), ...]], "flags": [["NORMAL" | "CONTROL", ...]]},
     "rotate": RotateAngle in 1/100 degree counter-clockwise (optional),
     "props": [(name, value)], values are int/float/bool/str or Enum/Struct/Marker below,
     "text": {"value": str, "props": [(name, value)]} (text shapes),
     "children": [spec, ...] (groups), "tag": source tag}

Ported in spirit from the Slidemax LibreOffice host (drawplan.py: pure plan layer, UNO layer separate), rewritten for
the InventaSketch draw plan contract (src/InventaSketch.Engine/DrawPlan.cs).
"""

import math
from collections import namedtuple

HMM_PER_PT = 2540.0 / 72.0
MM_PER_PT = 25.4 / 72.0

Enum = namedtuple("Enum", "type value")
Struct = namedtuple("Struct", "type fields")
# Line-end marker: LibreOffice marker name, polygon (list of (x, y) in marker units, tip at the top, y down), width in
# 1/100 mm (LibreOffice scales the polygon to this width), centre flag (line ends in the marker centre).
Marker = namedtuple("Marker", "name polygon curved width center")

SERVICES = {
    "rect": "com.sun.star.drawing.RectangleShape",
    "ellipse": "com.sun.star.drawing.EllipseShape",
    "line": "com.sun.star.drawing.LineShape",
    "polyline": "com.sun.star.drawing.PolyLineShape",
    "polygon": "com.sun.star.drawing.PolyPolygonShape",
    "open-bezier": "com.sun.star.drawing.OpenBezierShape",
    "closed-bezier": "com.sun.star.drawing.ClosedBezierShape",
    "text": "com.sun.star.drawing.CustomShape",
    "svg": "com.sun.star.drawing.GraphicObjectShape",
}

# Dash patterns of the engine (Style.DashArray: multiples of the stroke width, minimum width 0.18 mm) as LibreOffice
# LineDash values (style RECT, absolute lengths): (dots, dot length, dashes, dash length, distance) in widths.
DASHES = {
    "dash": (0, 0, 1, 6, 3),
    "longDash": (0, 0, 1, 12, 4),
    "dot": (1, 1, 0, 0, 2.5),
    "dashDot": (1, 2, 1, 16, 3),
    "dashDotDot": (2, 2, 1, 16, 3),
}

# Native line-end markers: (OOXML marker type, polygon, width factor). The polygons follow the engine's head geometry
# (ArrowHeads.Build: half width 0.38 x length; diamond 1.5 x length long and 0.9 x length wide; oval diameter
# 0.6 x length) in marker units (100 = head length, tip at the top). The marker type is the name prefix LibreOffice's
# OOXML import uses ("msArrowEnd 5"); its export maps such names to the PowerPoint arrow type and size, so .pptx files
# get native PowerPoint arrowheads (verified with LibreOffice 26.8).
MARKERS = {
    "triangle": ("msArrowEnd", [(38, 0), (76, 100), (0, 100)], 0.76),
    "half": ("msArrowEnd", [(38, 0), (76, 100), (0, 100)], 0.76),  # approximated by a triangle, as in PowerPoint COM
    "stealth": ("msArrowStealthEnd", [(38, 0), (76, 100), (38, 65), (0, 100)], 0.76),
    "diamond": ("msArrowDiamondEnd", [(45, 0), (90, 75), (45, 150), (0, 75)], 0.9),
    "oval": ("msArrowOvalEnd", None, 0.6),
    "open": ("msArrowOpenEnd", None, 0.76),
    "tick": ("msArrowOpenEnd", None, 0.76),  # approximated by an open arrow, as in PowerPoint COM
}


def ooxml_size_index(size):
    """OOXML width/length index of a plan arrow size as the PowerPoint COM host maps it (small: medium width and length;
    medium and large: wide and long): index = width * 3 + length + 1 with 0 = sm, 1 = med, 2 = lg."""
    return 5 if size == "small" else 9

ALIGN = {"left": "LEFT", "center": "CENTER", "right": "RIGHT"}
GENERIC_FONTS = {"sans-serif", "serif", "monospace", "cursive", "fantasy", "system-ui"}


def hmm(pt):
    """Points to 1/100 mm (integer, as the drawing API expects)."""
    return int(round(float(pt) * HMM_PER_PT))


def pt_of_hmm(value):
    return float(value) / HMM_PER_PT


def color(value):
    """'#RRGGBB' (or '#RGB') as an integer; None and 'none' stay None."""
    if not value or str(value).lower() == "none":
        return None
    v = str(value).lstrip("#")
    if len(v) == 3:
        v = "".join(c * 2 for c in v)
    try:
        return int(v[:6], 16)
    except ValueError:
        return 0


def rotate_angle(rotation):
    """Plan rotation (degrees clockwise on screen) as LibreOffice RotateAngle (1/100 degree counter-clockwise)."""
    if not rotation:
        return 0
    return int(round((-float(rotation)) % 360.0 * 100)) % 36000


def font_name(font):
    """First concrete family of a CSS-like font list ("Arial, sans-serif" -> "Arial"); None for generic families."""
    if not font:
        return None
    for part in str(font).split(","):
        name = part.strip().strip("'\"")
        if name and name.lower() not in GENERIC_FONTS:
            return name
    return None


def native_head_length_pt(size, weight):
    """Native head length as the engine assumes it (DrawPlanBuilder.NativeHeadLength): 3x (small) or 5x the weight."""
    return float(weight) * (3.0 if size == "small" else 5.0)


def _open_marker_polygon(thickness):
    """Open arrow (chevron) of the given arm thickness in marker units (head length 100, half width 38)."""
    t = max(4.0, min(30.0, thickness))
    arm = math.hypot(38.0, 100.0)
    dx = t * arm / 100.0   # horizontal width of an arm at the base
    dy = t * arm / 38.0    # vertical thickness at the apex
    return [(38.0, 0.0), (76.0, 100.0), (76.0 - dx, 100.0), (38.0, dy), (dx, 100.0), (0.0, 100.0)]


def _circle_polygon():
    """Circle of diameter 100 as four cubic Bézier arcs: list of (x, y) with control points, plus flags."""
    k = 0.5522847498 * 50
    c = 50.0
    pts = [(c, 0.0), (c + k, 0.0), (100.0, c - k), (100.0, c), (100.0, c + k), (c + k, 100.0), (c, 100.0),
           (c - k, 100.0), (0.0, c + k), (0.0, c), (0.0, c - k), (c - k, 0.0), (c, 0.0)]
    flags = ["NORMAL", "CONTROL", "CONTROL"] * 4 + ["NORMAL"]
    return pts, flags


def marker(head, size, weight_pt):
    """Native line-end marker for a plan arrowhead name, or None if LibreOffice has no equivalent."""
    entry = MARKERS.get(head)
    if entry is None or not weight_pt:
        return None
    kind, polygon, width_factor = entry
    length = native_head_length_pt(size, weight_pt)
    curved = None
    variant = "isk"
    if head in ("open", "tick"):
        # Arm thickness = line weight; 100 marker units = head length. The thickness is part of the name because a
        # document's marker table holds one polygon per name.
        thickness = max(4.0, min(30.0, 100.0 * weight_pt / length))
        polygon = _open_marker_polygon(thickness)
        variant = "isk%d" % int(round(thickness))
    elif head == "oval":
        polygon, curved = _circle_polygon()
    name = "%s %d %s" % (kind, ooxml_size_index(size), variant)
    return Marker(name, polygon, curved, max(1, hmm(length * width_factor)), False)


def line_props(stroke, closed=False):
    """Line properties for a plan stroke (None = no line). Arrowheads only on open shapes."""
    if not stroke or float(stroke.get("weight") or 0) <= 0:
        return [("LineStyle", Enum("com.sun.star.drawing.LineStyle", "NONE"))]
    weight = float(stroke["weight"])
    props = [("LineColor", color(stroke.get("color")) or 0), ("LineWidth", max(1, hmm(weight))),
             ("LineJoint", Enum("com.sun.star.drawing.LineJoint", "ROUND"))]
    dash = DASHES.get(stroke.get("dash") or "solid")
    if dash:
        w = max(weight, 0.18 / MM_PER_PT)  # the engine's minimum dash unit (0.18 mm)
        dots, dot_len, dashes, dash_len, distance = dash
        props += [("LineStyle", Enum("com.sun.star.drawing.LineStyle", "DASH")),
                  ("LineDash", Struct("com.sun.star.drawing.LineDash", {
                      "Style": Enum("com.sun.star.drawing.DashStyle", "RECT"),
                      "Dots": dots, "DotLen": hmm(dot_len * w), "Dashes": dashes, "DashLen": hmm(dash_len * w),
                      "Distance": hmm(distance * w)})),
                  ("LineCap", Enum("com.sun.star.drawing.LineCap", "BUTT"))]
    else:
        props += [("LineStyle", Enum("com.sun.star.drawing.LineStyle", "SOLID")),
                  ("LineCap", Enum("com.sun.star.drawing.LineCap", "ROUND"))]
    if not closed:
        size = stroke.get("arrowSize") or "medium"
        for attr, prefix in (("arrowStart", "LineStart"), ("arrowEnd", "LineEnd")):
            head = stroke.get(attr)
            if head:
                m = marker(head, size, weight)
                if m is not None:
                    props.append((prefix, m))
    return props


def fill_props(fill, opacity=None):
    """Fill properties: none, solid colour or native hatch (FillHatch, optionally on a background colour)."""
    fill = fill or {}
    kind = fill.get("kind") or "none"
    props = []
    if kind == "solid" and color(fill.get("color")) is not None:
        props += [("FillStyle", Enum("com.sun.star.drawing.FillStyle", "SOLID")), ("FillColor", color(fill["color"]))]
    elif kind == "hatch" and fill.get("hatch"):
        h = fill["hatch"]
        background = color(fill.get("color"))
        props += [("FillStyle", Enum("com.sun.star.drawing.FillStyle", "HATCH")),
                  ("FillHatch", Struct("com.sun.star.drawing.Hatch", {
                      "Style": Enum("com.sun.star.drawing.HatchStyle", "DOUBLE" if h.get("cross") else "SINGLE"),
                      "Color": color(h.get("color")) or 0,
                      "Distance": max(1, hmm(h.get("spacing") or 1)),
                      # Plan and LibreOffice both count hatch angles counter-clockwise; LibreOffice in 1/10 degree.
                      "Angle": int(round(float(h.get("angle") or 0) % 180.0 * 10))})),
                  ("FillBackground", background is not None)]
        if background is not None:
            props.append(("FillColor", background))
    else:
        props.append(("FillStyle", Enum("com.sun.star.drawing.FillStyle", "NONE")))
    if opacity is not None and float(opacity) < 1:
        props.append(("FillTransparence", int(round((1 - max(0.0, float(opacity))) * 100))))
    return props


def _box(op):
    x, y = float(op.get("x") or 0), float(op.get("y") or 0)
    w, h = max(0.0, float(op.get("w") or 0)), max(0.0, float(op.get("h") or 0))
    return {"x": hmm(x), "y": hmm(y), "w": max(1, hmm(w)), "h": max(1, hmm(h))}


def _rotated_points(op, unit_points):
    """Points of a unit shape (0..1 in the box) rotated about the box centre, in 1/100 mm."""
    x, y = float(op.get("x") or 0), float(op.get("y") or 0)
    w, h = float(op.get("w") or 0), float(op.get("h") or 0)
    cx, cy = x + w / 2, y + h / 2
    a = math.radians(float(op.get("rotation") or 0))
    ca, sa = math.cos(a), math.sin(a)
    out = []
    for u, v in unit_points:
        px, py = x + u * w - cx, y + v * h - cy
        out.append((hmm(cx + px * ca - py * sa), hmm(cy + px * sa + py * ca)))
    return out


def _with_opacity_line(props, opacity):
    if opacity is not None and float(opacity) < 1:
        props.append(("LineTransparence", int(round((1 - max(0.0, float(opacity))) * 100))))
    return props


def bezier_paths(segments):
    """Plan segments (M, L, C, Z) -> (coords, flags, closed) per sub-path; 1/100 mm."""
    paths = []
    coords, flags, closed = None, None, False
    start = None
    for s in segments or []:
        op = s.get("op")
        if op == "M":
            if coords and len(coords) > 1:
                paths.append((coords, flags, closed))
            start = (hmm(s["x"]), hmm(s["y"]))
            coords, flags, closed = [start], ["NORMAL"], False
        elif coords is None:
            continue
        elif op == "L":
            coords.append((hmm(s["x"]), hmm(s["y"])))
            flags.append("NORMAL")
        elif op == "C":
            coords += [(hmm(s.get("x1", s["x"])), hmm(s.get("y1", s["y"]))),
                       (hmm(s.get("x2", s["x"])), hmm(s.get("y2", s["y"]))), (hmm(s["x"]), hmm(s["y"]))]
            flags += ["CONTROL", "CONTROL", "NORMAL"]
        elif op == "Z":
            if start is not None and coords[-1] != start:
                coords.append(start)
                flags.append("NORMAL")
            closed = True
    if coords and len(coords) > 1:
        paths.append((coords, flags, closed))
    return paths


def op_spec(op, notes):
    """One plan op -> one spec (or None if there is nothing to draw)."""
    kind = op.get("op")
    tag = op.get("tag")
    opacity = op.get("opacity")
    if kind == "group":
        children = [c for c in (op_spec(x, notes) for x in op.get("children") or []) if c is not None]
        if not children:
            return None
        if len(children) == 1:
            return children[0]
        return {"kind": "group", "children": children, "tag": tag}
    if kind in ("rect", "ellipse"):
        spec = dict(_box(op), kind=kind, service=SERVICES[kind], tag=tag)
        props = _with_opacity_line(line_props(op.get("stroke"), closed=True), opacity) + fill_props(op.get("fill"), opacity)
        if kind == "rect" and op.get("radius"):
            props.append(("CornerRadius", hmm(op["radius"])))
        spec["props"] = props
        if op.get("rotation"):
            spec["rotate"] = rotate_angle(op["rotation"])
        return spec
    if kind == "shape":
        unit = [(0.5, 0.0), (1.0, 0.5), (0.5, 1.0), (0.0, 0.5)] if op.get("shape") == "diamond" else \
            [(0.5, 0.0), (1.0, 1.0), (0.0, 1.0)]
        return {"kind": "polygon", "service": SERVICES["polygon"], "points": [_rotated_points(op, unit)], "tag": tag,
                "props": _with_opacity_line(line_props(op.get("stroke"), closed=True), opacity) + fill_props(op.get("fill"), opacity)}
    if kind == "line":
        pts = [(hmm(op.get("x1") or 0), hmm(op.get("y1") or 0)), (hmm(op.get("x2") or 0), hmm(op.get("y2") or 0))]
        return {"kind": "line", "service": SERVICES["line"], "points": [pts], "tag": tag,
                "props": _with_opacity_line(line_props(op.get("stroke")), opacity)}
    if kind == "polyline":
        pts = [(hmm(p[0]), hmm(p[1])) for p in op.get("points") or []]
        if len(pts) < 2:
            return None
        closed = bool(op.get("closed"))
        k = "polygon" if closed else "polyline"
        props = _with_opacity_line(line_props(op.get("stroke"), closed=closed), opacity)
        if closed:
            props += fill_props(op.get("fill"), opacity)
        return {"kind": k, "service": SERVICES[k], "points": [pts], "tag": tag, "props": props}
    if kind == "freeform":
        paths = bezier_paths(op.get("segments"))
        if not paths:
            return None
        closed = bool(op.get("closed")) or all(p[2] for p in paths)
        has_fill = (op.get("fill") or {}).get("kind", "none") != "none"
        if not closed and has_fill:
            notes.append("open freeform with fill drawn without fill")
        service = SERVICES["closed-bezier" if closed else "open-bezier"]
        props = _with_opacity_line(line_props(op.get("stroke"), closed=closed), opacity)
        if closed:
            props += fill_props(op.get("fill"), opacity)
        return {"kind": "bezier", "service": service, "closed": closed, "tag": tag, "props": props,
                "bezier": {"coords": [p[0] for p in paths], "flags": [p[1] for p in paths]}}
    if kind == "text":
        spec = dict(_box(op), kind="text", service=SERVICES["text"], tag=tag)
        char = [("CharHeight", float(op.get("fontSize") or 10)),
                ("CharWeight", 150.0 if op.get("bold") else 100.0),
                ("CharPosture", Enum("com.sun.star.awt.FontSlant", "ITALIC" if op.get("italic") else "NONE")),
                ("CharUnderline", 1 if op.get("underline") else 0),
                ("CharColor", color(op.get("color")) or 0)]
        name = font_name(op.get("font"))
        if name:
            char.append(("CharFontName", name))
        spec["text"] = {"value": op.get("text") or "", "props": char,
                        "align": ALIGN.get(op.get("align") or "center", "CENTER")}
        spec["props"] = [("LineStyle", Enum("com.sun.star.drawing.LineStyle", "NONE")),
                         ("FillStyle", Enum("com.sun.star.drawing.FillStyle", "NONE")),
                         ("TextAutoGrowHeight", False), ("TextAutoGrowWidth", False), ("TextWordWrap", False),
                         ("TextLeftDistance", 0), ("TextRightDistance", 0), ("TextUpperDistance", 0),
                         ("TextLowerDistance", 0),
                         ("TextVerticalAdjust", Enum("com.sun.star.drawing.TextVerticalAdjust", "CENTER")),
                         ("TextHorizontalAdjust", Enum("com.sun.star.drawing.TextHorizontalAdjust", "BLOCK"))]
        if op.get("rotation"):
            spec["rotate"] = rotate_angle(op["rotation"])
        return spec
    if kind == "svg":
        if not op.get("svg"):
            return None
        notes.append("unit drawn as SVG picture")
        return dict(_box(op), kind="svg", service=SERVICES["svg"], svg=op["svg"], tag=tag, props=[])
    notes.append("unknown draw op '%s' skipped" % kind)
    return None


def unit_specs(unit):
    """Specs of a plan unit in drawing order and notes about approximations."""
    notes = list(unit.get("notes") or [])
    specs = [s for s in (op_spec(op, notes) for op in unit.get("ops") or []) if s is not None]
    return specs, notes


def count_shapes(specs):
    """Number of leaf shapes in a spec list (groups not counted)."""
    n = 0
    for s in specs:
        n += count_shapes(s["children"]) if s["kind"] == "group" else 1
    return n
