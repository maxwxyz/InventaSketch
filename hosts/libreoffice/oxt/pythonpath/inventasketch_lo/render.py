"""Shape specifications (plan.py) -> native UNO shapes; unit metadata on shapes and in document properties.

Counterpart of hosts/powerpoint-com/InventaSketch.AddIn/ComRenderer.cs. UNO shape creation follows the Slidemax
LibreOffice host (impress.create_shape: add the shape to the page first, then geometry, properties and text; text boxes
as CustomShape rectangles so word wrap can be switched off), translated and adapted to the InventaSketch plan.
"""

import uno
from com.sun.star.awt import Point, Size
from com.sun.star.beans import PropertyValue

from . import metadata
from .log import log
from .plan import Enum, Marker, Struct, pt_of_hmm, unit_specs

REMOVABLE = 128  # com.sun.star.beans.PropertyAttribute.REMOVABLE
PARA_ADJUST = {"LEFT": 0, "RIGHT": 1, "CENTER": 3}


def _pv(name, value):
    p = PropertyValue()
    p.Name = name
    p.Value = value
    return p


def to_uno(value):
    """Plain spec value -> UNO value (enums, structs; everything else unchanged)."""
    if isinstance(value, Enum):
        return uno.Enum(value.type, value.value)
    if isinstance(value, Struct):
        s = uno.createUnoStruct(value.type)
        for k, v in value.fields.items():
            setattr(s, k, to_uno(v))
        return s
    return value


def _bezier_coords(coords, flags):
    pp = uno.createUnoStruct("com.sun.star.drawing.PolyPolygonBezierCoords")
    pp.Coordinates = tuple(tuple(Point(int(round(x)), int(round(y))) for x, y in poly) for poly in coords)
    pp.Flags = tuple(tuple(uno.Enum("com.sun.star.drawing.PolygonFlags", f) for f in fl) for fl in flags)
    return pp


def _marker_coords(m):
    if m.curved:
        return _bezier_coords([m.polygon], [m.curved])
    return _bezier_coords([m.polygon + [m.polygon[0]]], [["NORMAL"] * (len(m.polygon) + 1)])


def apply_marker(doc, shape, prefix, m):
    """Native line end: the marker polygon (engine head geometry) registered under its OOXML-style name in the
    document's marker table (so the .pptx export writes the matching PowerPoint arrow type and size), then width and
    centre. Without a marker table the polygon is set directly (LibreOffice then names it itself)."""
    try:
        table = doc.createInstance("com.sun.star.drawing.MarkerTable")
        if not table.hasByName(m.name):
            table.insertByName(m.name, _marker_coords(m))
        shape.setPropertyValue(prefix + "Name", m.name)
    except Exception as ex:
        log("marker table: %s" % ex)
        shape.setPropertyValue(prefix, _marker_coords(m))
    shape.setPropertyValue(prefix + "Width", int(m.width))
    shape.setPropertyValue(prefix + "Center", bool(m.center))


def _set_props(doc, shape, props):
    for name, value in props:
        if isinstance(value, Marker):
            try:
                apply_marker(doc, shape, name, value)
            except Exception as ex:
                log("line end %s not set: %s" % (name, ex))
            continue
        try:
            shape.setPropertyValue(name, to_uno(value))
        except Exception as ex:
            log("property %s not set on %s: %s" % (name, shape.getShapeType(), ex))


def _svg_graphic(svg):
    ctx = uno.getComponentContext()
    smgr = ctx.ServiceManager
    stream = smgr.createInstanceWithContext("com.sun.star.io.SequenceInputStream", ctx)
    stream.initialize((uno.ByteSequence(svg.encode("utf-8")),))
    provider = smgr.createInstanceWithContext("com.sun.star.graphic.GraphicProvider", ctx)
    return provider.queryGraphic((_pv("InputStream", stream), _pv("MimeType", "image/svg+xml")))


def _set_text(shape, text):
    shape.setString(text["value"])
    cursor = shape.createTextCursor()
    cursor.gotoStart(False)
    cursor.gotoEnd(True)
    for name, value in text["props"]:
        try:
            cursor.setPropertyValue(name, to_uno(value))
        except Exception as ex:
            log("text property %s not set: %s" % (name, ex))
    try:
        cursor.setPropertyValue("ParaAdjust", PARA_ADJUST.get(text.get("align"), 3))
    except Exception as ex:
        log("ParaAdjust not set: %s" % ex)


def create_shape(doc, page, spec, ctx=None):
    """Creates the shape (or group) of one spec in ``page`` (a draw page or a group shape) and returns it.

    Groups are built as a GroupShape that is added first and then filled (not with XShapeGrouper.group, which records
    an undo action of its own: undoing it would leave the children loose on the page, because adding shapes through
    the API records none). The drawn units are undone as a whole by host._DrawnUnits."""
    kind = spec["kind"]
    if kind == "group":
        children = spec["children"]
        if len(children) == 1:
            return create_shape(doc, page, children[0], ctx)
        grp = doc.createInstance("com.sun.star.drawing.GroupShape")
        page.add(grp)
        for c in children:
            create_shape(doc, grp, c, ctx)
        return grp
    shape = doc.createInstance(spec["service"])
    page.add(shape)
    if kind in ("line", "polyline", "polygon"):
        shape.PolyPolygon = tuple(tuple(Point(x, y) for x, y in poly) for poly in spec["points"])
    elif kind == "bezier":
        shape.PolyPolygonBezier = _bezier_coords(spec["bezier"]["coords"], spec["bezier"]["flags"])
    else:
        shape.Position = Point(spec["x"], spec["y"])
        shape.Size = Size(spec["w"], spec["h"])
        if kind == "text":
            shape.CustomShapeGeometry = (_pv("Type", "rectangle"),)
        elif kind == "svg":
            shape.Graphic = _svg_graphic(spec["svg"])
    _set_props(doc, shape, spec.get("props") or [])
    if kind == "text":
        _set_text(shape, spec["text"])
    if spec.get("rotate"):
        shape.RotateAngle = int(spec["rotate"])
    return shape


def bounds_pt(shape):
    """Position and size (pt) and RotateAngle (1/100 degree counter-clockwise) of a unit shape."""
    p, s = shape.Position, shape.Size
    try:
        r = int(shape.RotateAngle)
    except Exception:
        r = 0
    return {"x": pt_of_hmm(p.X), "y": pt_of_hmm(p.Y), "w": pt_of_hmm(s.Width), "h": pt_of_hmm(s.Height), "r": r}


# ------------------------------------------------------------------ metadata


def _attr(value):
    a = uno.createUnoStruct("com.sun.star.xml.AttributeData")
    a.Type = "CDATA"
    a.Value = value
    a.Namespace = metadata.ATTR_NAMESPACE
    return a


def attribute_carrier(shape):
    """Shape that carries the UserDefinedAttributes of a unit: the shape itself, or for groups (which have no
    UserDefinedAttributes in LibreOffice) their first leaf shape."""
    while shape is not None and shape.getShapeType() == "com.sun.star.drawing.GroupShape":
        if shape.getCount() == 0:
            return None
        shape = shape.getByIndex(0)
    return shape


def write_attributes(shape, values):
    """UserDefinedAttributes (ODF attributes on the shape element) for {name: value}."""
    shape = attribute_carrier(shape)
    try:
        uda = shape.UserDefinedAttributes
        for name, value in values.items():
            if uda.hasByName(name):
                uda.replaceByName(name, _attr(value))
            else:
                uda.insertByName(name, _attr(value))
        shape.UserDefinedAttributes = uda
        return True
    except Exception as ex:
        log("UserDefinedAttributes not written: %s" % ex)
        return False


def read_attribute(shape, name):
    try:
        uda = attribute_carrier(shape).UserDefinedAttributes
        if uda is not None and uda.hasByName(name):
            return uda.getByName(name).Value
    except Exception:
        pass
    return None


def _user_props(doc):
    return doc.getDocumentProperties().getUserDefinedProperties()


def get_doc_property(doc, name):
    props = _user_props(doc)
    if props.getPropertySetInfo().hasPropertyByName(name):
        return str(props.getPropertyValue(name))
    return None


def set_doc_property(doc, base, value):
    """Stores ``value`` under ``base`` in chunks; removes surplus old chunks."""
    props = _user_props(doc)
    parts = metadata.split_value(value)
    for name, part in zip(metadata.chunk_names(base, len(parts)), parts):
        if props.getPropertySetInfo().hasPropertyByName(name):
            props.setPropertyValue(name, part)
        else:
            props.addProperty(name, REMOVABLE, part)
    i = len(parts)
    while props.getPropertySetInfo().hasPropertyByName("%s_%d" % (base, i)):
        props.removeProperty("%s_%d" % (base, i))
        i += 1


def remove_doc_property(doc, base):
    props = _user_props(doc)
    for name in [base] + ["%s_%d" % (base, i) for i in range(1, 1000)]:
        if not props.getPropertySetInfo().hasPropertyByName(name):
            if name != base:
                break
            continue
        props.removeProperty(name)


def doc_property_names(doc):
    return [p.Name for p in _user_props(doc).getPropertySetInfo().getProperties()]


def store_unit(doc, shape, unit):
    """Name, attributes and document mirror of a drawn unit."""
    shape.Name = metadata.unit_name(unit["type"], unit["id"])
    bounds = metadata.bounds_json(bounds_pt(shape))
    write_attributes(shape, {metadata.ATTR_ID: unit["id"], metadata.ATTR_TYPE: unit["type"],
                             metadata.ATTR_MODEL: unit["payload"], metadata.ATTR_BOUNDS: bounds})
    set_doc_property(doc, metadata.model_property(unit["id"]), unit["payload"])
    set_doc_property(doc, metadata.bounds_property(unit["id"]), bounds)


def read_unit(doc, shape):
    """{"id", "type", "payload", "bounds", "source"} of a unit shape, or None if it is not an InventaSketch unit."""
    parsed = metadata.parse_name(shape.Name if hasattr(shape, "Name") else "")
    unit_id = read_attribute(shape, metadata.ATTR_ID) or (parsed[1] if parsed else None)
    if not unit_id:
        return None
    unit_type = read_attribute(shape, metadata.ATTR_TYPE) or (parsed[0] if parsed else "")
    payload = read_attribute(shape, metadata.ATTR_MODEL)
    bounds = read_attribute(shape, metadata.ATTR_BOUNDS)
    source = "attributes"
    if not payload:
        payload = metadata.join_chunks(lambda n: get_doc_property(doc, n), metadata.model_property(unit_id))
        bounds = get_doc_property(doc, metadata.bounds_property(unit_id))
        source = "document"
    if not payload:
        return None
    if source == "attributes" and unit_type and (parsed is None or parsed[1] != unit_id):
        # LibreOffice renames a duplicated shape ("…eb12" → "…eb 1"); in .pptx the name carries the id, so restore it.
        try:
            shape.Name = metadata.unit_name(unit_type, unit_id)
            log("unit name restored: %s" % shape.Name)
        except Exception as ex:
            log("unit name not restored: %s" % ex)
    return {"id": unit_id, "type": unit_type, "payload": payload, "bounds": metadata.parse_bounds(bounds),
            "source": source}


def draw_unit(doc, page, unit, ctx=None):
    """Draws one plan unit and stores its metadata; returns (shape, notes)."""
    specs, notes = unit_specs(unit)
    if not specs:
        return None, notes
    top = create_shape(doc, page, specs[0] if len(specs) == 1 else {"kind": "group", "children": specs}, ctx)
    store_unit(doc, top, unit)
    return top, notes
