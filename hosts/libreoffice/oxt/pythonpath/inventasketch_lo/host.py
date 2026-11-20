"""Document side of the extension: target page, insertion point, selection, insert and edit flows.

Every user action uses a short-lived engine session (createSession -> insert/importCompact -> edit -> drawPlan ->
closeSession), so the engine process keeps no per-document state and may be restarted at any time. The page of the
session has the size of the LibreOffice page, so draw plan coordinates (pt) are page coordinates. One undo step per
action (UndoManager context).
"""

from . import i18n, metadata, render
from .log import log

IMPRESS = "com.sun.star.presentation.PresentationDocument"
DRAW = "com.sun.star.drawing.DrawingDocument"
CALC = "com.sun.star.sheet.SpreadsheetDocument"


class HostError(Exception):
    """Problem the user should see (message in the UI language)."""


def doc_kind(doc):
    """'impress', 'draw', 'calc' or None."""
    try:
        if doc.supportsService(IMPRESS):
            return "impress"
        if doc.supportsService(DRAW):
            return "draw"
        if doc.supportsService(CALC):
            return "calc"
    except Exception:
        pass
    return None


def target(controller):
    """(doc, page, insertion point in mm, session page size in mm) for the current view."""
    doc = controller.getModel()
    kind = doc_kind(doc)
    if kind in ("impress", "draw"):
        page = controller.getCurrentPage()
        w, h = page.Width / 100.0, page.Height / 100.0
        return doc, page, (w / 2.0, h / 2.0), (w, h)
    if kind == "calc":
        sheet = controller.getActiveSheet()
        page = sheet.getDrawPage()
        cx, cy = _calc_visible_centre(controller, sheet)
        # The draw page of a sheet has no fixed size; the session page just has to contain the insertion point.
        return doc, page, (cx, cy), (max(297.0, 2 * cx), max(210.0, 2 * cy))
    raise HostError(i18n.text("notSupported"))


def _calc_visible_centre(controller, sheet):
    """Centre of the visible cell range in mm (sheet coordinates)."""
    try:
        r = controller.getVisibleRange()
        first = sheet.getCellByPosition(r.StartColumn, r.StartRow)
        last = sheet.getCellByPosition(r.EndColumn, r.EndRow)
        p0, p1, s1 = first.Position, last.Position, last.Size
        return (p0.X + p1.X + s1.Width) / 200.0, (p0.Y + p1.Y + s1.Height) / 200.0
    except Exception as ex:
        log("visible range not available: %s" % ex)
        return 100.0, 70.0


def _undo_manager(doc):
    try:
        return doc.getUndoManager()
    except Exception:
        return None


class _UndoStep:
    def __init__(self, doc, title):
        self.manager = _undo_manager(doc)
        self.title = title
        self.doc = doc

    def __enter__(self):
        if self.manager is not None:
            self.manager.enterUndoContext(self.title)
        try:
            self.doc.lockControllers()
        except Exception:
            pass
        return self

    def __exit__(self, *exc):
        try:
            self.doc.unlockControllers()
        except Exception:
            pass
        if self.manager is not None:
            try:
                self.manager.leaveUndoContext()
            except Exception as ex:
                log("leaveUndoContext: %s" % ex)
        return False


def _session(engine, size_mm):
    state = engine.call("createSession", {"pageWidth": size_mm[0], "pageHeight": size_mm[1],
                                          "lang": i18n.language(), "includeSvg": False})
    return state["session"]


def _close(engine, session):
    try:
        engine.call("closeSession", {"session": session})
    except Exception as ex:
        log("closeSession: %s" % ex)


def _select(controller, shapes):
    try:
        if len(shapes) == 1:
            controller.select(shapes[0])
        elif shapes:
            import uno
            ctx = uno.getComponentContext()
            coll = ctx.ServiceManager.createInstanceWithContext("com.sun.star.drawing.ShapeCollection", ctx)
            for s in shapes:
                coll.add(s)
            controller.select(coll)
    except Exception as ex:
        log("select: %s" % ex)


def _draw_units(doc, page, plan):
    drawn = []
    for unit in plan.get("units") or []:
        shape, notes = render.draw_unit(doc, page, unit)
        if notes:
            log("unit %s (%s): %s" % (unit["id"], unit["type"], "; ".join(notes)))
        if shape is not None:
            drawn.append(shape)
    return drawn


def insert(engine, controller, item_id, x_mm=None, y_mm=None, name=None):
    """Inserts a library item (centre of the page/visible area unless x/y in mm are given); returns the new shapes."""
    doc, page, centre, size = target(controller)
    x = centre[0] if x_mm is None else float(x_mm)
    y = centre[1] if y_mm is None else float(y_mm)
    session = _session(engine, (max(size[0], x * 2), max(size[1], y * 2)))
    try:
        state = engine.call("insert", {"session": session, "itemId": item_id, "x": x, "y": y, "includeSvg": False})
        plan = engine.call("drawPlan", {"session": session, "ids": state["selection"]})
    finally:
        _close(engine, session)
    with _UndoStep(doc, i18n.text("undoInsert") % (name or item_id)):
        drawn = _draw_units(doc, page, plan)
    _select(controller, drawn)
    return drawn


def selected_unit_shape(controller):
    """The selected InventaSketch unit shape (first selected shape with a unit name or attributes), or None."""
    try:
        sel = controller.getSelection()
    except Exception:
        return None
    if sel is None:
        return None
    shapes = []
    if hasattr(sel, "getCount") and hasattr(sel, "getByIndex") and not hasattr(sel, "getShapeType"):
        shapes = [sel.getByIndex(i) for i in range(sel.getCount())]
    elif hasattr(sel, "getShapeType"):
        shapes = [sel]
    for s in shapes:
        try:
            if metadata.parse_name(s.Name) or render.read_attribute(s, metadata.ATTR_ID):
                return s
        except Exception:
            continue
    return None


def _page_of(controller, shape):
    doc, page, _, size = target(controller)
    return doc, page, size


def describe(engine, controller):
    """Inspector info of the selected unit for the sidebar: {"name", "views": [(id, label)], "view", "unit"}; None
    if no unit is selected."""
    shape = selected_unit_shape(controller)
    if shape is None:
        return None
    doc = controller.getModel()
    unit = render.read_unit(doc, shape)
    if unit is None:
        raise HostError(i18n.text("noModel"))
    _, _, size = _page_of(controller, shape)
    session = _session(engine, size)
    try:
        state = engine.call("importCompact", {"session": session, "payload": unit["payload"], "includeSvg": False})
        ids = [o["id"] for o in state["objects"]]
        info = engine.call("inspector", {"session": session, "ids": ids[:1], "lang": i18n.language()})
    finally:
        _close(engine, session)
    return {"unit": unit, "name": info.get("typeName") or unit["type"], "view": info.get("view"),
            "views": [(v["id"], v["label"]) for v in info.get("views") or []], "source": unit["source"]}


def edit(engine, controller, method=None, params=None, label=None):
    """Re-renders the selected unit from its stored model after an optional engine edit (e.g. setView, applyAction).
    A native move/resize since the last drawing is applied first (hostTransform). Returns the new shapes."""
    shape = selected_unit_shape(controller)
    if shape is None:
        raise HostError(i18n.text("noUnit"))
    doc, page, size = _page_of(controller, shape)
    unit = render.read_unit(doc, shape)
    if unit is None:
        raise HostError(i18n.text("noModel"))
    current = render.bounds_pt(shape)
    session = _session(engine, size)
    try:
        state = engine.call("importCompact", {"session": session, "payload": unit["payload"], "includeSvg": False})
        ids = [o["id"] for o in state["objects"]]
        if unit.get("bounds") and metadata.transform_needed(unit["bounds"], current):
            request = {"session": session, "ids": ids, "includeSvg": False}
            request.update(metadata.host_transform(unit["bounds"], current))
            engine.call("hostTransform", request)
        if method:
            request = {"session": session, "ids": ids, "includeSvg": False}
            request.update(params or {})
            engine.call(method, request)
        state = engine.call("state", {"session": session, "includeSvg": False})
        plan = engine.call("drawPlan", {"session": session, "ids": [o["id"] for o in state["objects"]]})
    finally:
        _close(engine, session)
    with _UndoStep(doc, i18n.text("undoEdit") % (label or unit["type"])):
        z = shape.ZOrder
        old_id = unit["id"]
        page.remove(shape)
        drawn = _draw_units(doc, page, plan)
        new_ids = set()
        for s in drawn:
            parsed = metadata.parse_name(s.Name)
            if parsed:
                new_ids.add(parsed[1])
            if parsed and parsed[1] == old_id:
                try:
                    s.ZOrder = z
                except Exception:
                    pass
        if old_id not in new_ids:
            render.remove_doc_property(doc, metadata.model_property(old_id))
            render.remove_doc_property(doc, metadata.bounds_property(old_id))
    _select(controller, drawn)
    return drawn


def cleanup_properties(doc):
    """Removes document mirror properties of units that are on no page any more (deleted units)."""
    alive = set()
    pages = doc.getDrawPages() if hasattr(doc, "getDrawPages") else None
    if pages is None and hasattr(doc, "getSheets"):
        sheets = doc.getSheets()
        pages = [sheets.getByIndex(i).getDrawPage() for i in range(sheets.getCount())]
    elif pages is not None:
        pages = [pages.getByIndex(i) for i in range(pages.getCount())]
    for page in pages or []:
        for i in range(page.getCount()):
            parsed = metadata.parse_name(page.getByIndex(i).Name)
            if parsed:
                alive.add(parsed[1])
    removed = metadata.orphaned_properties(render.doc_property_names(doc), alive)
    props = doc.getDocumentProperties().getUserDefinedProperties()
    for name in removed:
        try:
            props.removeProperty(name)
        except Exception:
            pass
    return removed
